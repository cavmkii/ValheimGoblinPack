using UnityEngine;

namespace GoblinPack
{
    /// <summary>
    /// Per-client view of world-level state. The server fills this in via <see cref="Rpc.State"/>
    /// broadcasts; rage is also set directly by the chat trigger RPC.
    /// </summary>
    internal static class GoblinState
    {
        public static bool JoeKnown;
        public static Vector3 JoePos;
        public static bool SeanKnown;
        public static Vector3 SeanPos;

        /// <summary>World time (seconds) until which Joe is hostile to every player.</summary>
        public static double RageUntil;
        /// <summary>Player id of whoever said the rage phrase; Joe heads for them.</summary>
        public static long RageTarget;
        public static Vector3 RageOrigin;

        public static double Now => ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : 0d;

        public static bool IsRaging => Now < RageUntil;

        public static void Reset()
        {
            JoeKnown = false;
            SeanKnown = false;
            RageUntil = 0d;
            RageTarget = 0L;
        }
    }

    /// <summary>ZDO keys used by this mod. Prefixed to avoid colliding with vanilla or other mods.</summary>
    internal static class Keys
    {
        public const string Provokers = "gp_provokers";
        public const string StealCooldown = "gp_steal_cd";
        public const string Stash = "gp_stash";
        public const string DamageMult = "gp_dmg";
        public const string Level = "gp_level";
        public const string FightEnd = "gp_fight_end";
        public const string FightCooldown = "gp_fight_cd";
        public const string PlayerLevel = "gp_plevel";
        // Vanilla BaseAI persists its wander anchor under this key.
        public const string SpawnPoint = "spawnpoint";
    }
}
