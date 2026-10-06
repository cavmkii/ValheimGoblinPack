using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Managers;
using Jotunn.Utils;

namespace GoblinPack
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    internal class GoblinPackPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "cavmkii.goblinpack";
        public const string PluginName = "GoblinPack";
        public const string PluginVersion = "0.1.1";

        internal static GoblinPackPlugin Instance;
        internal static ManualLogSource Log;

        /// <summary>Names of patch classes that failed to apply (game update changed a signature).</summary>
        internal static readonly HashSet<string> FailedPatches = new HashSet<string>();

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            Cfg.Bind(Config);
            Content.Register();
            Commands.Register();
            ApplyPatches();
            SelfCheck.Run();

            Log.LogInfo($"{PluginName} {PluginVersion} loaded");
        }

        /// <summary>
        /// Patches each class separately so a single broken signature after a game update
        /// disables one feature instead of the whole mod.
        /// </summary>
        private void ApplyPatches()
        {
            _harmony = new Harmony(PluginGuid);
            foreach (Type type in typeof(GoblinPackPlugin).Assembly.GetTypes())
            {
                if (type.GetCustomAttributes(typeof(HarmonyPatch), false).Length == 0)
                {
                    continue;
                }

                try
                {
                    _harmony.CreateClassProcessor(type).Patch();
                }
                catch (Exception e)
                {
                    FailedPatches.Add(type.Name);
                    Log.LogWarning($"Patch {type.Name} failed to apply, that feature is disabled: {e.Message}");
                }
            }
        }

        private void Update()
        {
            if (ZNet.instance == null || ZNetScene.instance == null)
            {
                return;
            }

            try
            {
                PowerLevel.TickLocalPlayer();
                WorldDirector.Tick();
            }
            catch (Exception e)
            {
                Log.LogError(e);
            }
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
