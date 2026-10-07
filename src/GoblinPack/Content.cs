using System;
using System.Collections.Generic;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GoblinPack
{
    /// <summary>Prefabs, items and status effects, registered once vanilla prefabs are available.</summary>
    internal static class Content
    {
        public const string JoePrefab = "GP_Joe";
        public const string SeanPrefab = "GP_Sean";

        private static readonly string[] SeanBases = { "DvergerMageIce", "DvergerMage", "Dverger" };

        private static readonly List<Trader.TradeItem> Stock = new List<Trader.TradeItem>();
        private static readonly List<Trader.TradeItem> LegacyStock = new List<Trader.TradeItem>();
        private static bool _legacy;

        public static void Register()
        {
            PrefabManager.OnVanillaPrefabsAvailable += AddContent;
        }

        private static void AddContent()
        {
            // Fires on every return to the main menu; only build once.
            PrefabManager.OnVanillaPrefabsAvailable -= AddContent;
            try
            {
                AddItems();
                AddJoe();
                AddSean();
            }
            catch (Exception e)
            {
                GoblinPackPlugin.Log.LogError($"Failed to build GoblinPack content: {e}");
            }
        }

        // ------------------------------------------------------------------ creatures

        private static void AddJoe()
        {
            var config = new CreatureConfig { Name = "Joe", Faction = Character.Faction.PlainsMonsters };
            var creature = new CustomCreature(JoePrefab, "Goblin", config);
            GameObject prefab = creature.Prefab;

            prefab.transform.localScale *= Mathf.Clamp(Cfg.JoeScale.Value, 0.3f, 3f);
            Character character = prefab.GetComponent<Character>();
            character.m_name = "Joe";
            character.m_boss = false;

            MonsterAI ai = prefab.GetComponent<MonsterAI>();
            ai.m_randomMoveRange = 8f;
            ai.m_randomMoveInterval = 2f;
            // Fulings normally smash player buildings on sight; Joe is a nuisance, not a raid.
            ai.m_attackPlayerObjects = false;
            // Player attacks only land on enemies or "aggravatable" creatures (how passive Dvergr can be hit).
            // Joe isn't a player enemy until provoked, so without this nobody could hit him at all.
            ai.m_aggravatable = true;

            EnsurePersistent(prefab);
            prefab.AddComponent<JoeBrain>();
            CreatureManager.Instance.AddCreature(creature);
        }

        private static void AddSean()
        {
            string basePrefab = Array.Find(SeanBases, name => PrefabManager.Instance.GetPrefab(name) != null);
            if (basePrefab == null)
            {
                GoblinPackPlugin.Log.LogError("No Dvergr prefab found to build Sean from.");
                return;
            }

            var config = new CreatureConfig { Name = "Sean", Faction = Character.Faction.Dverger };
            var creature = new CustomCreature(SeanPrefab, basePrefab, config);
            GameObject prefab = creature.Prefab;

            Character character = prefab.GetComponent<Character>();
            character.m_name = "Sean";
            character.m_health = Cfg.SeanHealth.Value;
            character.m_boss = false;

            BaseAI ai = prefab.GetComponent<BaseAI>();
            ai.m_randomMoveRange = 5f;
            ai.m_randomMoveInterval = 4f;
            if (ai is MonsterAI monsterAI)
            {
                monsterAI.m_attackPlayerObjects = false;
            }

            Trader trader = prefab.AddComponent<Trader>();
            trader.m_name = "Sean";
            trader.m_items = new List<Trader.TradeItem>(Stock);
            if (Cfg.SellWeatherGear.Value)
            {
                trader.m_items.AddRange(LegacyStock);
            }
            R.Set(trader, "m_standRange", 6f);
            R.Set(trader, "m_greetRange", 5f);
            R.Set(trader, "m_byeRange", 7f);
            R.Set(trader, "m_randomTalk", new List<string>(Lines.SeanPitch));
            R.Set(trader, "m_randomGreets", new List<string>(Lines.SeanGreet));
            R.Set(trader, "m_randomGoodbye", new List<string>(Lines.SeanBye));
            R.Set(trader, "m_randomStartTrade", new List<string>(Lines.SeanGreet));
            R.Set(trader, "m_randomBuy", new List<string>(Lines.SeanBuy));
            R.Set(trader, "m_randomSell", new List<string>(Lines.SeanSell));
            // Speech is driven by SeanBrain; keep the vanilla idle chatter effectively off.
            R.Set(trader, "m_randomTalkInterval", 100000f);

            EnsurePersistent(prefab);
            prefab.AddComponent<SeanBrain>();
            CreatureManager.Instance.AddCreature(creature);
        }

        /// <summary>Only persistent ZNetViews are written to the world save.</summary>
        private static void EnsurePersistent(GameObject prefab)
        {
            ZNetView view = prefab.GetComponent<ZNetView>();
            if (view == null)
            {
                GoblinPackPlugin.Log.LogError($"{prefab.name} has no ZNetView; it can't be saved.");
                return;
            }
            if (!view.m_persistent)
            {
                GoblinPackPlugin.Log.LogInfo($"{prefab.name}: base prefab wasn't persistent; forcing it so it's saved with the world.");
                view.m_persistent = true;
            }
        }

        // ------------------------------------------------------------------ items Sean sells

        private static void AddItems()
        {
            Stock.Clear();
            LegacyStock.Clear();

            // Sean's shop, in display order.
            AddSoda();
            AddMerch();
            AddGlock();
            AddRestrainingOrder();
            AddDiddyOil();
            AddSixtySeven();

            // The original weather gear. Still registered so existing copies don't vanish from
            // inventories; only sold when Sean.SellWeatherGear is on.
            _legacy = true;
            AddCharm("GP_StormCharm", "Stormcaller's Charm",
                "A bead of trapped thunder. Lightning bends around the wearer, and stamina returns faster in the rush of the storm.",
                220, HitData.DamageType.Lightning, staminaRegen: 1.15f);
            AddCharm("GP_FrostCharm", "Hoarfrost Pendant",
                "Cold to the touch, warm to the soul. Frost resistance, and the mountains stop biting quite so hard.",
                260, HitData.DamageType.Frost, staminaRegen: 1f);
            AddCharm("GP_SunCharm", "Sunshard Talisman",
                "A sliver of a summer afternoon. Fire resistance and a little extra healing.",
                240, HitData.DamageType.Fire, staminaRegen: 1f, healthRegen: 1.15f);
            AddBottledWeather("GP_BottledSunshine", "Bottled Sunshine",
                "Uncork for five minutes of clear skies. Only you will see them. Sean insists this is a feature.",
                90, "Clear", 300f);
            AddBottledWeather("GP_BottledStorm", "Bottled Thunderstorm",
                "For when you want the mood without the commitment. Three minutes of storm, just for you.",
                60, "ThunderStorm", 180f);
            AddWeapon("GP_ThunderclapAxe", "AxeIron", "Thunderclap Axe",
                "An iron axe that hums before rain. Adds lightning damage.", 450,
                d => { d.m_lightning += 25f; return d; });
            AddWeapon("GP_HailstoneMace", "MaceIron", "Hailstone Mace",
                "Heavy as a hailstorm and twice as rude. Adds frost damage.", 500,
                d => { d.m_frost += 22f; return d; });
            AddWeapon("GP_GaleSpear", "SpearBronze", "Gale Spear",
                "Thrown with the wind at your back. Adds lightning damage and knockback.", 320,
                d => { d.m_lightning += 15f; return d; }, attackForce: 2f);
            AddWeapon("GP_SquallBow", "BowFineWood", "Squall Bow",
                "Strung with rain-soaked sinew. Adds frost damage to every shot.", 550,
                d => { d.m_frost += 12f; return d; });
            _legacy = false;
        }

        /// <summary>
        /// "Restraining Order": a belt (utility slot, like Megingjord). Joe must keep his distance from
        /// the wearer (see RestrainingOrder / JoeBrain). The equip effect has no stats; it just puts an
        /// icon in the buff bar so the wearer can see the order is in force.
        /// </summary>
        private static void AddRestrainingOrder()
        {
            var item = new CustomItem(RestrainingOrder.PrefabName, "BeltStrength");
            ItemDrop.ItemData.SharedData shared = item.ItemDrop.m_itemData.m_shared;
            shared.m_name = "Restraining Order";
            shared.m_description = "A belt with the court order stapled to it. Joe has to stay 15 metres away from you. " +
                                   "Void if you hit him. Does not cover the Baby Wars.";
            shared.m_itemType = ItemDrop.ItemData.ItemType.Utility;
            shared.m_weight = 1f;

            SE_Stats order = ScriptableObject.CreateInstance<SE_Stats>();
            order.name = "GP_SE_RestrainingOrder";
            order.m_name = "Restraining Order";
            order.m_tooltip = "Joe must stay 15 metres away from you.";
            order.m_icon = shared.m_icons != null && shared.m_icons.Length > 0 ? shared.m_icons[0] : null;
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(order, false));
            shared.m_equipStatusEffect = order;

            ItemManager.Instance.AddItem(item);
            AddStock(item, 300);
        }

        /// <summary>"Diddy Oil": a potion: faster movement and a bigger parry bonus.</summary>
        private static void AddDiddyOil()
        {
            var item = new CustomItem("GP_DiddyOil", "MeadHealthMinor");
            ItemDrop.ItemData.SharedData shared = item.ItemDrop.m_itemData.m_shared;
            shared.m_name = "Diddy Oil";
            shared.m_description = "Slippery. +25% movement speed and a much stronger parry for 5 minutes.";

            SE_Stats oil = ScriptableObject.CreateInstance<SE_Stats>();
            oil.name = "GP_SE_DiddyOil";
            oil.m_name = "Diddy Oil";
            oil.m_tooltip = "+25% movement speed, +100% parry bonus.";
            oil.m_icon = shared.m_icons != null && shared.m_icons.Length > 0 ? shared.m_icons[0] : null;
            oil.m_ttl = 300f;
            oil.m_speedModifier = 0.25f;
            oil.m_timedBlockBonus = 1f;
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(oil, false));
            shared.m_consumeStatusEffect = oil;

            ItemManager.Instance.AddItem(item);
            AddStock(item, 120, stack: 3);
        }

        /// <summary>"67": end-game fist weapon, built on the Flesh Rippers claws.</summary>
        private static void AddSixtySeven()
        {
            var item = new CustomItem("GP_SixtySeven", "FistFenrirClaw");
            ItemDrop.ItemData.SharedData shared = item.ItemDrop.m_itemData.m_shared;
            shared.m_name = "67";
            shared.m_description = "Six. Seven. End-game fists. Sean won't say where he got them.";
            var damage = new HitData.DamageTypes { m_slash = 160f, m_blunt = 67f, m_lightning = 30f };
            shared.m_damages = damage;
            shared.m_damagesPerLevel = new HitData.DamageTypes { m_slash = 7f, m_blunt = 6f, m_lightning = 2f };
            shared.m_attackForce = Mathf.Max(shared.m_attackForce, 67f);
            shared.m_maxDurability = 670f;
            shared.m_durabilityPerLevel = 67f;
            ItemManager.Instance.AddItem(item);
            AddStock(item, 6767);
        }

        // ------------------------------------------------------------------ Sean's merch line

        /// <summary>"Soda": a drink-bottle model turned into food: +35 health, +150 stamina.</summary>
        private static void AddSoda()
        {
            var item = new CustomItem("GP_Soda", "MeadStaminaMinor");
            ItemDrop.ItemData.SharedData shared = item.ItemDrop.m_itemData.m_shared;
            shared.m_name = "Soda";
            shared.m_description = "Sean's own brand. Tastes like a thunderstorm. +35 health, +150 stamina.";
            // Meads work through a status effect; food works through these fields. Make it food.
            shared.m_consumeStatusEffect = null;
            shared.m_food = 35f;
            shared.m_foodStamina = 150f;
            shared.m_foodEitr = 0f;
            shared.m_foodRegen = 2f;
            shared.m_foodBurnTime = 1200f;
            ItemManager.Instance.AddItem(item);
            AddStock(item, 25, stack: 5);
        }

        /// <summary>"Merch": a black chest piece with no stats at all.</summary>
        private static void AddMerch()
        {
            var item = new CustomItem("GP_Merch", "ArmorLeatherChest");
            ItemDrop.ItemData.SharedData shared = item.ItemDrop.m_itemData.m_shared;
            shared.m_name = "Merch";
            shared.m_description = "Official Sean merch. It's black. It does nothing. It's drip.";
            shared.m_armor = 0f;
            shared.m_armorPerLevel = 0f;
            shared.m_movementModifier = 0f;
            shared.m_useDurability = false;
            shared.m_weight = 1f;
            shared.m_equipStatusEffect = null;

            // Worn look comes from the armor material; the dropped look from the prefab's renderers.
            shared.m_armorMaterial = Blacken(shared.m_armorMaterial);
            foreach (Renderer renderer in item.ItemPrefab.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = Blacken(materials[i]);
                }
                renderer.sharedMaterials = materials;
            }

            ItemManager.Instance.AddItem(item);
            AddStock(item, 60);
        }

        private static Material Blacken(Material original)
        {
            if (original == null)
            {
                return null;
            }
            var black = new Material(original) { name = original.name + "_GPBlack" };
            if (black.HasProperty("_Color"))
            {
                black.color = new Color(0.07f, 0.07f, 0.07f, 1f);
            }
            else
            {
                GoblinPackPlugin.Log.LogWarning($"Merch: material {original.name} has no _Color; it may not look black.");
            }
            return black;
        }

        /// <summary>"Glock": an Arbalest with the reload removed, so it fires as fast as you click.</summary>
        private static void AddGlock()
        {
            var item = new CustomItem("GP_Glock", "CrossbowArbalest");
            ItemDrop.ItemData.SharedData shared = item.ItemDrop.m_itemData.m_shared;
            shared.m_name = "Glock";
            shared.m_description = "Semi-automatic. Sean says it's for 'weather emergencies'. Uses bolts.";
            Attack attack = shared.m_attack;
            attack.m_requiresReload = false;
            attack.m_reloadTime = 0f;
            attack.m_reloadStaminaDrain = 0f;
            attack.m_attackStamina = Mathf.Min(attack.m_attackStamina, 4f);
            ItemManager.Instance.AddItem(item);
            AddStock(item, 750);
        }

        private static void AddCharm(string id, string name, string description, int price,
            HitData.DamageType resist, float staminaRegen, float healthRegen = 1f)
        {
            var item = new CustomItem(id, "BeltStrength");
            ItemDrop.ItemData.SharedData shared = item.ItemDrop.m_itemData.m_shared;
            shared.m_name = name;
            shared.m_description = description;

            SE_Stats effect = ScriptableObject.CreateInstance<SE_Stats>();
            effect.name = id + "_SE";
            effect.m_name = name;
            effect.m_tooltip = description;
            effect.m_icon = shared.m_icons != null && shared.m_icons.Length > 0 ? shared.m_icons[0] : null;
            effect.m_mods = new List<HitData.DamageModPair>
            {
                new HitData.DamageModPair { m_type = resist, m_modifier = HitData.DamageModifier.Resistant },
            };
            if (!Mathf.Approximately(staminaRegen, 1f))
            {
                R.Set(effect, "m_staminaRegenMultiplier", staminaRegen);
            }
            if (!Mathf.Approximately(healthRegen, 1f))
            {
                R.Set(effect, "m_healthRegenMultiplier", healthRegen);
            }
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(effect, false));
            shared.m_equipStatusEffect = effect;

            ItemManager.Instance.AddItem(item);
            AddStock(item, price);
        }

        private static void AddBottledWeather(string id, string name, string description, int price, string environment, float duration)
        {
            var item = new CustomItem(id, "MeadTasty");
            ItemDrop.ItemData.SharedData shared = item.ItemDrop.m_itemData.m_shared;
            shared.m_name = name;
            shared.m_description = description;

            SE_BottledWeather effect = ScriptableObject.CreateInstance<SE_BottledWeather>();
            effect.name = id + "_SE";
            effect.m_name = name;
            effect.m_tooltip = description;
            effect.m_icon = shared.m_icons != null && shared.m_icons.Length > 0 ? shared.m_icons[0] : null;
            effect.m_ttl = duration;
            effect.Environment = environment;
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(effect, false));
            shared.m_consumeStatusEffect = effect;

            ItemManager.Instance.AddItem(item);
            AddStock(item, price, stack: 3);
        }

        private static void AddWeapon(string id, string basePrefab, string name, string description, int price,
            Func<HitData.DamageTypes, HitData.DamageTypes> addDamage, float attackForce = 1f)
        {
            var item = new CustomItem(id, basePrefab);
            ItemDrop.ItemData.SharedData shared = item.ItemDrop.m_itemData.m_shared;
            shared.m_name = name;
            shared.m_description = description;
            shared.m_damages = addDamage(shared.m_damages);
            shared.m_attackForce *= attackForce;

            ItemManager.Instance.AddItem(item);
            AddStock(item, price);
        }

        private static void AddStock(CustomItem item, int price, int stack = 1)
        {
            (_legacy ? LegacyStock : Stock).Add(new Trader.TradeItem { m_prefab = item.ItemDrop, m_stack = stack, m_price = price });
        }
    }

    /// <summary>Forces a weather environment for the local player while active.</summary>
    public class SE_BottledWeather : StatusEffect
    {
        public string Environment = "Clear";

        public override void Setup(Character character)
        {
            base.Setup(character);
            if (character == Player.m_localPlayer && EnvMan.instance != null)
            {
                EnvMan.instance.SetForceEnvironment(Environment);
            }
        }

        public override void Stop()
        {
            base.Stop();
            if (m_character == Player.m_localPlayer && EnvMan.instance != null)
            {
                EnvMan.instance.SetForceEnvironment("");
            }
        }
    }
}
