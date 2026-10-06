using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace GoblinPack
{
    /// <summary>
    /// Safety net for Joe and Sean dying. In testing they could reach 0 health and keep standing there
    /// instead of dying and disappearing. If one sits at 0 health for over a second, run the game's own
    /// death routine (effects, ragdoll, drops), logging any error it throws; if the object still isn't
    /// gone shortly after, remove it outright. The WorldDirector then sees it's gone and starts the
    /// respawn timer.
    /// </summary>
    internal class DeathWatch
    {
        private static readonly MethodInfo OnDeathMethod = AccessTools.Method(typeof(Character), "OnDeath");

        private readonly Character _character;
        private readonly ZNetView _nview;
        private float _zeroSince = -1f;
        private float _forcedAt = -1f;

        public DeathWatch(Character character, ZNetView nview)
        {
            _character = character;
            _nview = nview;
        }

        /// <summary>Call on the owner each think tick. Returns true when the NPC is dead or dying (skip other logic).</summary>
        public bool Tick()
        {
            bool markedDead = _character.IsDead();
            if (!markedDead && _character.GetHealth() > 0f)
            {
                _zeroSince = -1f;
                return false;
            }
            if (markedDead && _forcedAt < 0f)
            {
                // The game already ran its death routine; only the removal can be missing.
                _forcedAt = Time.time + 1f;
            }

            if (_zeroSince < 0f)
            {
                _zeroSince = Time.time;
                return true; // Give the game's own death check a moment first.
            }

            if (_forcedAt < 0f && Time.time - _zeroSince > 1f)
            {
                _forcedAt = Time.time;
                GoblinPackPlugin.Log.LogWarning($"{_character.m_name} is at 0 health but didn't die; running the death routine.");
                try
                {
                    OnDeathMethod?.Invoke(_character, null);
                }
                catch (Exception e)
                {
                    Exception root = e is TargetInvocationException && e.InnerException != null ? e.InnerException : e;
                    GoblinPackPlugin.Log.LogError($"{_character.m_name}'s death routine failed: {root}");
                }
            }
            else if (_forcedAt >= 0f && Time.time - _forcedAt > 2f && _nview.IsValid())
            {
                GoblinPackPlugin.Log.LogWarning($"{_character.m_name} still present after dying; removing it.");
                _forcedAt = float.MaxValue; // Only once.
                ZNetScene.instance.Destroy(_character.gameObject);
            }
            return true;
        }
    }
}
