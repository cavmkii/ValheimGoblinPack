using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace GoblinPack
{
    /// <summary>
    /// When it starts snowing where you are, your character says a line in normal chat (so it shows
    /// above your head for nearby players too). Once per snowfall, with a cooldown.
    /// </summary>
    internal static class SnowQuip
    {
        // Read via reflection: the game's runtime enforces field visibility.
        private static readonly FieldInfo CurrentEnv = AccessTools.Field(typeof(EnvMan), "m_currentEnv");

        private static bool _wasSnowing;
        private static float _nextCheck;
        private static float _lastSaid = -9999f;

        public static void TickLocalPlayer()
        {
            if (Time.time < _nextCheck || !Cfg.SnowLineEnabled.Value)
            {
                return;
            }
            _nextCheck = Time.time + 2f;

            Player player = Player.m_localPlayer;
            if (player == null || player.IsDead() || EnvMan.instance == null || Chat.instance == null || CurrentEnv == null)
            {
                return;
            }

            bool snowing = CurrentEnv.GetValue(EnvMan.instance) is EnvSetup env
                           && env.m_name != null
                           && env.m_name.IndexOf("snow", StringComparison.OrdinalIgnoreCase) >= 0;

            if (snowing && !_wasSnowing && Time.time - _lastSaid > Cfg.SnowLineCooldownMinutes.Value * 60f)
            {
                _lastSaid = Time.time;
                Chat.instance.SendText(Talker.Type.Normal, Cfg.SnowLine.Value);
            }
            _wasSnowing = snowing;
        }
    }
}
