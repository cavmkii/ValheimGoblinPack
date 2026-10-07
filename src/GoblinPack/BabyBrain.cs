using System.Collections.Generic;
using UnityEngine;

namespace GoblinPack
{
    /// <summary>
    /// A baby soldier in Joe's army: a shrunken Fuling. It charges the nearest player who isn't
    /// wearing a Baby Bonnet (the vanilla AI does the fighting once it sees them), scales with
    /// progression like Joe, and carries whatever curses were offered at the war cauldron.
    /// </summary>
    public class BabyBrain : MonoBehaviour
    {
        internal const string CurseKey = "gp_curse";

        private static readonly Dictionary<Character, BabyBrain> ByCharacter = new Dictionary<Character, BabyBrain>();

        private Character _character;
        private BaseAI _ai;
        private ZNetView _nview;
        private Wander _wander;
        private DeathWatch _deathWatch;
        private Curse _curses;
        private bool _cursesApplied;
        private float _nextThink;
        private float _nextScale;
        private float _nextPuke;
        private float _pukeUntil;

        public static bool TryGet(Character character, out BabyBrain baby)
        {
            baby = null;
            return character != null && ByCharacter.TryGetValue(character, out baby);
        }

        private void Awake()
        {
            _character = GetComponent<Character>();
            _ai = GetComponent<BaseAI>();
            _nview = GetComponent<ZNetView>();
            if (_character == null || _ai == null || _nview == null || _nview.GetZDO() == null)
            {
                enabled = false;
                return;
            }
            _wander = new Wander(_ai, _nview, Heightmap.Biome.None);
            _deathWatch = new DeathWatch(_character, _nview);
            Puke.Register(_nview, _character);
            ByCharacter[_character] = this;
            _nextPuke = Time.time + Random.Range(6f, 15f);
        }

        private void OnDestroy()
        {
            if (_character != null)
            {
                ByCharacter.Remove(_character);
            }
        }

        private void Update()
        {
            if (!_nview.IsValid())
            {
                return;
            }
            // Curses change movement stats; apply them on every client so whoever owns the baby uses them.
            if (!_cursesApplied)
            {
                _cursesApplied = true;
                ApplyCurses((Curse)_nview.GetZDO().GetInt(CurseKey, 0));
            }
            if (!_nview.IsOwner() || Time.time < _nextThink)
            {
                return;
            }
            _nextThink = Time.time + 1f;

            if (_deathWatch.Tick())
            {
                return;
            }
            UpdateScaling();

            if ((_curses & Curse.Puke) != 0 && UpdatePuke())
            {
                return;
            }

            Player target = ClosestEnemyPlayer(80f);
            if (target != null)
            {
                _wander.SetAnchor(target.transform.position, 2f);
                _ai.m_randomMoveInterval = 0.5f;
            }
            else
            {
                _wander.SetAnchor(BabyWar.Camp, 10f);
                _ai.m_randomMoveInterval = 3f;
            }
        }

        private Player ClosestEnemyPlayer(float range)
        {
            Player best = null;
            float bestDistance = range;
            foreach (Player player in Player.GetAllPlayers())
            {
                if (player == null || player.IsDead() || WornItems.Has(player, Worn.BabyBonnet))
                {
                    continue;
                }
                float d = Vector3.Distance(player.transform.position, transform.position);
                if (d < bestDistance)
                {
                    best = player;
                    bestDistance = d;
                }
            }
            return best;
        }

        /// <summary>Used by the IsEnemy patch: babies leave Bonnet wearers alone.</summary>
        internal static bool IsHostileTo(Player player)
        {
            return !WornItems.Has(player, Worn.BabyBonnet);
        }

        private void ApplyCurses(Curse curses)
        {
            _curses = curses;
            float speed = 1f;
            if ((curses & Curse.Slow) != 0)
            {
                speed *= 0.5f;
            }
            if ((curses & Curse.Heavy) != 0)
            {
                speed *= 0.7f;
            }
            if (speed < 1f)
            {
                _character.m_speed *= speed;
                _character.m_walkSpeed *= speed;
                _character.m_runSpeed *= speed;
                _character.m_turnSpeed *= Mathf.Lerp(1f, speed, 0.5f);
            }
        }

        private bool UpdatePuke()
        {
            if (Time.time < _pukeUntil)
            {
                _wander.SetAnchor(transform.position, 0.5f);
                return true;
            }
            if (Time.time < _nextPuke)
            {
                return false;
            }
            _nextPuke = Time.time + Random.Range(8f, 16f);
            _pukeUntil = Time.time + Puke.Seconds;
            _wander.SetAnchor(transform.position, 0.5f);
            Puke.Trigger(_nview);
            return true;
        }

        private void UpdateScaling()
        {
            if (Time.time < _nextScale)
            {
                return;
            }
            _nextScale = Time.time + 20f;

            float level = PowerLevel.Evaluate(transform.position);
            float hpMult = Mathf.Lerp(Cfg.HealthMultMin.Value, Cfg.HealthMultMax.Value, level) * Cfg.BabyHealthScale.Value;
            float dmgMult = Mathf.Lerp(Cfg.DamageMultMin.Value, Cfg.DamageMultMax.Value, level) * Cfg.BabyDamageScale.Value;
            if ((_curses & Curse.Heavy) != 0)
            {
                dmgMult *= 0.6f;
            }

            _nview.GetZDO().Set(Keys.DamageMult, dmgMult);

            float newMax = Mathf.Max(5f, _character.m_health * hpMult);
            float oldMax = _character.GetMaxHealth();
            if (Mathf.Abs(newMax - oldMax) > 1f && _character.GetHealth() > 0f)
            {
                float ratio = oldMax > 0f ? Mathf.Clamp01(_character.GetHealth() / oldMax) : 1f;
                _character.SetMaxHealth(newMax);
                _character.SetHealth(newMax * ratio);
            }
        }
    }

    /// <summary>
    /// One of Sean's Security guards: a Dvergr who stays near Sean (or the war) and fights the babies.
    /// He leaves people in Sean's Merch alone and goes after everyone else.
    /// </summary>
    public class SecurityBrain : MonoBehaviour
    {
        private static readonly Dictionary<Character, SecurityBrain> ByCharacter = new Dictionary<Character, SecurityBrain>();

        private Character _character;
        private ZNetView _nview;
        private Wander _wander;
        private DeathWatch _deathWatch;
        private float _nextThink;

        public static bool TryGet(Character character, out SecurityBrain guard)
        {
            guard = null;
            return character != null && ByCharacter.TryGetValue(character, out guard);
        }

        internal static bool IsHostileTo(Player player)
        {
            return !WornItems.Has(player, Worn.Merch);
        }

        private void Awake()
        {
            _character = GetComponent<Character>();
            BaseAI ai = GetComponent<BaseAI>();
            _nview = GetComponent<ZNetView>();
            if (_character == null || ai == null || _nview == null || _nview.GetZDO() == null)
            {
                enabled = false;
                return;
            }
            _wander = new Wander(ai, _nview, Heightmap.Biome.None);
            _deathWatch = new DeathWatch(_character, _nview);
            ByCharacter[_character] = this;
        }

        private void OnDestroy()
        {
            if (_character != null)
            {
                ByCharacter.Remove(_character);
            }
        }

        private void Update()
        {
            if (!_nview.IsValid() || !_nview.IsOwner() || Time.time < _nextThink)
            {
                return;
            }
            _nextThink = Time.time + 1f;
            if (_deathWatch.Tick())
            {
                return;
            }

            SeanBrain sean = SeanBrain.FindNear(transform.position, 120f);
            Vector3 post = sean != null ? sean.transform.position : BabyWar.Camp;
            _wander.SetAnchor(post, sean != null ? 6f : 15f);
        }
    }
}
