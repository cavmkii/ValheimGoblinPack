using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace GoblinPack
{
    // Each class is patched on its own (see GoblinPackPlugin.ApplyPatches), so a game update that
    // breaks one signature only disables that feature.

    internal static class Guard
    {
        /// <summary>Logs a GoblinPack error from inside a patch without letting it break the game's method.</summary>
        public static void Report(Exception e)
        {
            GoblinPackPlugin.Log.LogError($"GoblinPack patch error (game continues normally): {e}");
        }
    }

    [HarmonyPatch(typeof(Game), "Start")]
    internal static class RegisterRpcsPatch
    {
        private static void Postfix() => Rpc.Register();
    }

    /// <summary>
    /// Joe is only an enemy to players who provoked him, or to everyone during a rage. This decides
    /// Joe-vs-player outright (in both directions), overriding vanilla faction and aggravation rules.
    /// </summary>
    [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.IsEnemy), typeof(Character), typeof(Character))]
    internal static class JoeNeutralPatch
    {
        private static void Postfix(Character a, Character b, ref bool __result)
        {
            try
            {
                if (JoeBrain.TryGet(a, out JoeBrain joe) && b is Player playerB)
                {
                    __result = joe.IsHostileTo(playerB);
                }
                else if (JoeBrain.TryGet(b, out joe) && a is Player playerA)
                {
                    __result = joe.IsHostileTo(playerA);
                }
            }
            catch (Exception e)
            {
                Guard.Report(e);
            }
        }
    }

    /// <summary>Record who hit Joe, on Joe's owner, before the AI reacts to the hit.</summary>
    [HarmonyPatch(typeof(Character), "RPC_Damage")]
    internal static class JoeProvokePatch
    {
        private static void Prefix(Character __instance, HitData hit)
        {
            try
            {
                if (hit == null)
                {
                    return;
                }
                bool isJoe = JoeBrain.TryGet(__instance, out JoeBrain joe);
                if (isJoe || SeanBrain.TryGet(__instance, out _))
                {
                    Character attacker = hit.GetAttacker();
                    string who = attacker is Player p ? p.GetPlayerName() : attacker != null ? attacker.m_name : "environment/unknown";
                    LastHits[__instance] = $"{who} for {hit.GetTotalDamage():F0}";
                }
                if (isJoe && hit.GetAttacker() is Player player)
                {
                    joe.OnStruckBy(player);
                }
            }
            catch (Exception e)
            {
                Guard.Report(e);
            }
        }

        /// <summary>Last hit taken by Joe or Sean, logged when they die.</summary>
        internal static readonly Dictionary<Character, string> LastHits = new Dictionary<Character, string>();
    }

    /// <summary>
    /// Damage rules: Joe's outgoing damage scales with level; Sean shrugs off ordinary monsters;
    /// Joe and Sean can't finish each other off (players still can).
    /// </summary>
    [HarmonyPatch(typeof(Character), nameof(Character.Damage))]
    internal static class DamageRulesPatch
    {
        private const float DuelFloor = 0.15f;

        private static void Prefix(Character __instance, HitData hit)
        {
            try
            {
                if (hit == null)
                {
                    return;
                }
                Character attacker = hit.GetAttacker();

                if (attacker != null && !Fight.ApplyingSpell && JoeBrain.TryGet(attacker, out _))
                {
                    hit.m_damage.Modify(JoeBrain.DamageMultiplier(attacker));
                }

                bool targetIsSean = SeanBrain.TryGet(__instance, out _);
                bool targetIsJoe = JoeBrain.TryGet(__instance, out _);
                bool attackerIsJoe = attacker != null && JoeBrain.TryGet(attacker, out _);
                bool attackerIsSean = attacker != null && SeanBrain.TryGet(attacker, out _);

                if (targetIsSean && !(attacker is Player) && !attackerIsJoe)
                {
                    hit.m_damage.Modify(Cfg.SeanNonPlayerDamageTaken.Value);
                }

                if ((targetIsSean && attackerIsJoe) || (targetIsJoe && attackerIsSean))
                {
                    float floor = __instance.GetMaxHealth() * DuelFloor;
                    float room = __instance.GetHealth() - floor;
                    float total = hit.GetTotalDamage();
                    if (room <= 0f)
                    {
                        hit.m_damage.Modify(0f);
                    }
                    else if (total > room)
                    {
                        hit.m_damage.Modify(room / total);
                    }
                }
            }
            catch (Exception e)
            {
                Guard.Report(e);
            }
        }
    }

    /// <summary>Joe drops everything he stole when he dies.</summary>
    [HarmonyPatch(typeof(Character), "OnDeath")]
    internal static class JoeDeathPatch
    {
        private static void Prefix(Character __instance)
        {
            try
            {
                bool isJoe = JoeBrain.TryGet(__instance, out JoeBrain joe);
                if (!isJoe && !SeanBrain.TryGet(__instance, out _))
                {
                    return;
                }

                JoeProvokePatch.LastHits.TryGetValue(__instance, out string lastHit);
                JoeProvokePatch.LastHits.Remove(__instance);
                GoblinPackPlugin.Log.LogInfo($"{__instance.m_name} died at {__instance.transform.position.ToString("F0")}; last hit by {lastHit ?? "nothing recorded"}.");

                if (isJoe)
                {
                    joe.SayDeathLine();
                    joe.DropStash();
                }
            }
            catch (Exception e)
            {
                Guard.Report(e);
            }
        }
    }

    /// <summary>Show Joe's level, and give Sean a trade prompt (the Character hover wins over the Trader's).</summary>
    [HarmonyPatch(typeof(Character), nameof(Character.GetHoverName))]
    internal static class HoverNamePatch
    {
        private static void Postfix(Character __instance, ref string __result)
        {
            try
            {
                if (JoeBrain.TryGet(__instance, out _))
                {
                    __result = $"{__result} (Lv {JoeBrain.DisplayLevel(__instance)})";
                }
            }
            catch (Exception e)
            {
                Guard.Report(e);
            }
        }
    }

    [HarmonyPatch(typeof(Character), nameof(Character.GetHoverText))]
    internal static class SeanHoverPatch
    {
        private static void Postfix(Character __instance, ref string __result)
        {
            try
            {
                if (SeanBrain.TryGet(__instance, out SeanBrain sean) && sean.TryGetComponent(out Trader trader))
                {
                    __result = trader.GetHoverText();
                }
            }
            catch (Exception e)
            {
                Guard.Report(e);
            }
        }
    }

    /// <summary>
    /// Vanilla Trader.Update drives Haldor-style idle animations and head tracking, which a walking
    /// Dvergr doesn't have. Sean's greetings come from SeanBrain instead; trading still works because
    /// Interact and the store UI don't depend on Update.
    /// </summary>
    [HarmonyPatch(typeof(Trader), "Update")]
    internal static class SeanTraderUpdatePatch
    {
        private static bool Prefix(Trader __instance)
        {
            try
            {
                return __instance.GetComponent<SeanBrain>() == null;
            }
            catch (Exception e)
            {
                Guard.Report(e);
                return true;
            }
        }
    }

    /// <summary>Saying a rage phrase in chat sends Joe into a fury.</summary>
    [HarmonyPatch(typeof(Chat), nameof(Chat.SendText))]
    internal static class RageChatPatch
    {
        private static void Prefix(string text)
        {
            try
            {
                Player player = Player.m_localPlayer;
                if (player == null || string.IsNullOrEmpty(text) || ZRoutedRpc.instance == null)
                {
                    return;
                }

                string said = Normalize(text);
                bool triggered = Cfg.RagePhrases.Value
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(Normalize)
                    .Any(phrase => phrase.Length > 0 && said.Contains(phrase));

                if (triggered)
                {
                    Rpc.SendRage(player);
                }
            }
            catch (Exception e)
            {
                Guard.Report(e);
            }
        }

        /// <summary>Lowercase letters and digits only, so "Baby Wars!", "baby-wars" and "babywars" all match.</summary>
        internal static string Normalize(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(char.ToLowerInvariant(c));
                }
            }
            return sb.ToString();
        }
    }
}
