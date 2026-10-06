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

        public static GameObject CraterSmall;
        public static GameObject CraterMedium;
        public static GameObject CraterLarge;

        private static readonly List<Trader.TradeItem> Stock = new List<Trader.TradeItem>();

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
                AddCraters();
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

            prefab.AddComponent<SeanBrain>();
            CreatureManager.Instance.AddCreature(creature);
        }

        // ------------------------------------------------------------------ items Sean sells

        private static void AddItems()
        {
            Stock.Clear();

            // Weather charms: utility-slot trinkets with resistances.
            AddCharm("GP_StormCharm", "Stormcaller's Charm",
                "A bead of trapped thunder. Lightning bends around the wearer, and stamina returns faster in the rush of the storm.",
                220, HitData.DamageType.Lightning, staminaRegen: 1.15f);
            AddCharm("GP_FrostCharm", "Hoarfrost Pendant",
                "Cold to the touch, warm to the soul. Frost resistance, and the mountains stop biting quite so hard.",
                260, HitData.DamageType.Frost, staminaRegen: 1f);
            AddCharm("GP_SunCharm", "Sunshard Talisman",
                "A sliver of a summer afternoon. Fire resistance and a little extra healing.",
                240, HitData.DamageType.Fire, staminaRegen: 1f, healthRegen: 1.15f);

            // Bottled weather: consumables that change the sky for the drinker.
            AddBottledWeather("GP_BottledSunshine", "Bottled Sunshine",
                "Uncork for five minutes of clear skies. Only you will see them. Sean insists this is a feature.",
                90, "Clear", 300f);
            AddBottledWeather("GP_BottledStorm", "Bottled Thunderstorm",
                "For when you want the mood without the commitment. Three minutes of storm, just for you.",
                60, "ThunderStorm", 180f);

            // Weather-forged weapons.
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
            Stock.Add(new Trader.TradeItem { m_prefab = item.ItemDrop, m_stack = stack, m_price = price });
        }

        // ------------------------------------------------------------------ craters

        /// <summary>
        /// Terrain edits replicate by prefab hash, so craters must be real registered prefabs rather
        /// than TerrainOps built at runtime. Jotunn registers any prefab with a TerrainOp into ObjectDB.
        /// </summary>
        private static void AddCraters()
        {
            float depth = Mathf.Max(0f, Cfg.CraterScale.Value);
            CraterSmall = MakeCrater("GP_CraterSmall", 2.5f, 1.2f * depth);
            CraterMedium = MakeCrater("GP_CraterMedium", 4f, 2.2f * depth);
            CraterLarge = MakeCrater("GP_CraterLarge", 6f, 3.5f * depth);
        }

        private static GameObject MakeCrater(string name, float radius, float depth)
        {
            GameObject prefab = PrefabManager.Instance.CreateEmptyPrefab(name, false);
            if (prefab == null)
            {
                return null;
            }

            // CreateEmptyPrefab makes a cube; we only want the terrain operation.
            Object.DestroyImmediate(prefab.GetComponent<Collider>());
            Object.DestroyImmediate(prefab.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(prefab.GetComponent<MeshFilter>());

            TerrainOp op = prefab.AddComponent<TerrainOp>();
            object settings = R.Get(op, "m_settings");
            if (settings == null)
            {
                GoblinPackPlugin.Log.LogWarning("TerrainOp.m_settings not found; craters disabled.");
                Object.DestroyImmediate(prefab);
                return null;
            }

            R.Set(settings, "m_level", false);
            R.Set(settings, "m_raise", true);
            R.Set(settings, "m_raiseRadius", radius);
            R.Set(settings, "m_raisePower", 2f);
            R.Set(settings, "m_raiseDelta", -depth);
            R.Set(settings, "m_smooth", true);
            R.Set(settings, "m_smoothRadius", radius + 1.5f);
            R.Set(settings, "m_smoothPower", 3f);
            R.Set(settings, "m_paintCleared", true);
            R.Set(settings, "m_paintRadius", radius + 1f);
            R.SetEnum(settings, "m_paintType", "Dirt");
            // Settings may be a struct in some versions: write the (boxed) copy back.
            R.Set(op, "m_settings", settings);

            PrefabManager.Instance.AddPrefab(new CustomPrefab(prefab, false));
            return prefab;
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
