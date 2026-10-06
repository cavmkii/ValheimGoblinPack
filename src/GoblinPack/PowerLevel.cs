using System.Linq;
using UnityEngine;

namespace GoblinPack
{
    /// <summary>
    /// Valheim has no character level, so "player level" is defined here as a 0..1 value from
    /// either the players' skills or world boss progression (see <see cref="ScalingMode"/>).
    /// </summary>
    internal static class PowerLevel
    {
        private static readonly string[] BossKeys =
        {
            "defeated_eikthyr",
            "defeated_gdking",
            "defeated_bonemass",
            "defeated_dragon",
            "defeated_goblinking",
            "defeated_queen",
            "defeated_fader",
        };

        private const float ScanRadius = 80f;
        private static float _nextReport;

        /// <summary>
        /// Skills only exist on the owning client, so each client publishes its own player's level
        /// on the player's ZDO, where whoever owns Joe can read it.
        /// </summary>
        public static void TickLocalPlayer()
        {
            if (Time.time < _nextReport)
            {
                return;
            }
            _nextReport = Time.time + 20f;

            Player player = Player.m_localPlayer;
            ZNetView view = player != null ? player.GetComponent<ZNetView>() : null;
            if (view == null || !view.IsValid() || !view.IsOwner())
            {
                return;
            }

            var levels = player.GetSkills().GetSkillList()
                .Select(skill => skill.m_level)
                .OrderByDescending(level => level)
                .Take(5)
                .ToList();
            float average = levels.Count > 0 ? levels.Average() : 0f;
            view.GetZDO().Set(Keys.PlayerLevel, average);
        }

        /// <summary>0..1 power level at a position.</summary>
        public static float Evaluate(Vector3 position)
        {
            float bosses = BossFactor();
            float skills = SkillFactor(position);
            switch (Cfg.Scaling.Value)
            {
                case ScalingMode.Bosses:
                    return bosses;
                case ScalingMode.Skills:
                    return skills;
                default:
                    return Mathf.Max(bosses, skills);
            }
        }

        private static float BossFactor()
        {
            if (ZoneSystem.instance == null)
            {
                return 0f;
            }
            int defeated = BossKeys.Count(key => ZoneSystem.instance.GetGlobalKey(key));
            return (float)defeated / BossKeys.Length;
        }

        private static float SkillFactor(Vector3 position)
        {
            float best = 0f;
            foreach (Player player in Player.GetAllPlayers())
            {
                if (Vector3.Distance(player.transform.position, position) > ScanRadius)
                {
                    continue;
                }
                ZNetView view = player.GetComponent<ZNetView>();
                if (view == null || !view.IsValid())
                {
                    continue;
                }
                best = Mathf.Max(best, view.GetZDO().GetFloat(Keys.PlayerLevel, 0f));
            }
            return Mathf.Clamp01(best / 100f);
        }
    }
}
