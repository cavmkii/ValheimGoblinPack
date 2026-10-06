using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace GoblinPack
{
    /// <summary>
    /// Joe's behaviour layered on a vanilla Fuling's MonsterAI. All decisions run on the client that
    /// owns Joe's ZDO; shared state (provocation, stash, fight timers) lives on the ZDO so ownership
    /// can move between players without Joe forgetting anything.
    /// </summary>
    public class JoeBrain : MonoBehaviour
    {
        internal static readonly List<JoeBrain> Instances = new List<JoeBrain>();
        private static readonly Dictionary<Character, JoeBrain> ByCharacter = new Dictionary<Character, JoeBrain>();

        private const Heightmap.Biome Forbidden = Heightmap.Biome.AshLands | Heightmap.Biome.DeepNorth | Heightmap.Biome.Ocean;

        private Character _character;
        private MonsterAI _ai;
        private ZNetView _nview;
        private Wander _wander;
        private Fight _fight;
        private DeathWatch _deathWatch;

        private float _nextThink;
        private float _nextTaunt;
        private float _nextScale;
        private float _fleeUntil;
        private float _nextPuke;
        private float _pukeUntil;
        private string _provokerCacheSource;
        private Dictionary<long, double> _provokerCache = new Dictionary<long, double>();

        internal Character Character => _character;
        internal ZNetView NView => _nview;
        internal Wander Wander => _wander;

        public static bool TryGet(Character character, out JoeBrain joe)
        {
            joe = null;
            return character != null && ByCharacter.TryGetValue(character, out joe);
        }

        private void Awake()
        {
            _character = GetComponent<Character>();
            _ai = GetComponent<MonsterAI>();
            _nview = GetComponent<ZNetView>();
            if (_character == null || _ai == null || _nview == null || _nview.GetZDO() == null)
            {
                // Placement ghosts and other local-only copies.
                enabled = false;
                return;
            }

            _wander = new Wander(_ai, _nview, Forbidden);
            _fight = new Fight(this);
            _deathWatch = new DeathWatch(_character, _nview);
            Speech.Register(_nview, 1.4f);
            Puke.Register(_nview, _character);
            _fight.RegisterRpcs();

            Instances.Add(this);
            ByCharacter[_character] = this;
            _nextTaunt = Time.time + Random.Range(5f, 12f);
            _nextPuke = Time.time + Random.Range(20f, 40f);
        }

        private void OnDestroy()
        {
            Instances.Remove(this);
            if (_character != null)
            {
                ByCharacter.Remove(_character);
            }
        }

        private void Update()
        {
            if (!_nview.IsValid() || !_nview.IsOwner())
            {
                return;
            }
            if (Time.time < _nextThink)
            {
                return;
            }
            _nextThink = Time.time + 0.5f;

            if (_deathWatch.Tick())
            {
                return;
            }

            UpdateScaling();

            if (_fight.Update())
            {
                return;
            }
            _fight.TryStart();

            if (GoblinState.IsRaging)
            {
                UpdateRage();
            }
            else if (!HasActiveProvokers())
            {
                if (UpdatePuke())
                {
                    return;
                }
                UpdateCalm();
            }

            UpdateTaunts();
        }

        /// <summary>Every so often, when someone's around to see it, Joe stops and throws up. Returns true while he's busy.</summary>
        private bool UpdatePuke()
        {
            if (Time.time < _pukeUntil)
            {
                _wander.SetAnchor(transform.position, 0.5f);
                return true;
            }
            if (!Cfg.PukeEnabled.Value || Time.time < _nextPuke)
            {
                return false;
            }

            float min = Mathf.Max(5f, Cfg.PukeIntervalMin.Value);
            _nextPuke = Time.time + Random.Range(min, Mathf.Max(min, Cfg.PukeIntervalMax.Value));
            if (Player.GetClosestPlayer(transform.position, 40f) == null)
            {
                return false; // Nobody to gross out.
            }

            _pukeUntil = Time.time + Puke.Seconds;
            _wander.SetAnchor(transform.position, 0.5f);
            Puke.Trigger(_nview);
            Speech.Say(_nview, Lines.Pick(Lines.JoePuke));
            return true;
        }

        // ---------------------------------------------------------------- hostility

        /// <summary>Used by the BaseAI.IsEnemy patch. Joe ignores players unless provoked or enraged.</summary>
        internal bool IsHostileTo(Player player)
        {
            if (GoblinState.IsRaging)
            {
                return true;
            }
            return ProvokedUntil(player.GetPlayerID()) > GoblinState.Now;
        }

        /// <summary>Called on the owner, before damage is applied, so the AI already sees the attacker as an enemy.</summary>
        internal void OnStruckBy(Player attacker)
        {
            if (!_nview.IsValid() || !_nview.IsOwner())
            {
                return;
            }

            long id = attacker.GetPlayerID();
            bool wasHostile = ProvokedUntil(id) > GoblinState.Now;
            var provokers = ReadProvokers();
            provokers[id] = GoblinState.Now + Cfg.ProvokeSeconds.Value;
            WriteProvokers(provokers);

            if (!wasHostile && !GoblinState.IsRaging)
            {
                Speech.Say(_nview, Lines.Pick(Lines.JoeProvoked, attacker.GetPlayerName()), true);
            }
        }

        internal void OnRage(string speakerName)
        {
            if (!_nview.IsValid() || !_nview.IsOwner())
            {
                return;
            }
            Speech.Say(_nview, Lines.Pick(Lines.JoeRage, speakerName), true);
            Speech.Announce(_nview, "Joe heard that. Joe is FURIOUS.");
        }

        private bool HasActiveProvokers()
        {
            double now = GoblinState.Now;
            return ReadProvokers().Values.Any(until => until > now);
        }

        private double ProvokedUntil(long playerId)
        {
            return ReadProvokers().TryGetValue(playerId, out double until) ? until : 0d;
        }

        // Format: "playerId:untilSeconds;playerId:untilSeconds". Parsed lazily and cached per string value,
        // because IsEnemy is called many times per second.
        private Dictionary<long, double> ReadProvokers()
        {
            string raw = _nview.IsValid() ? _nview.GetZDO().GetString(Keys.Provokers, "") : "";
            if (raw == _provokerCacheSource)
            {
                return _provokerCache;
            }

            var result = new Dictionary<long, double>();
            foreach (string entry in raw.Split(';'))
            {
                string[] parts = entry.Split(':');
                if (parts.Length == 2
                    && long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out long id)
                    && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double until))
                {
                    result[id] = until;
                }
            }
            _provokerCacheSource = raw;
            _provokerCache = result;
            return result;
        }

        private void WriteProvokers(Dictionary<long, double> provokers)
        {
            double now = GoblinState.Now;
            string raw = string.Join(";", provokers
                .Where(kv => kv.Value > now)
                .Select(kv => kv.Key.ToString(CultureInfo.InvariantCulture) + ":" + kv.Value.ToString("F1", CultureInfo.InvariantCulture)));
            _nview.GetZDO().Set(Keys.Provokers, raw);
        }

        // ---------------------------------------------------------------- calm: wander, look for Sean, pickpocket

        private void UpdateCalm()
        {
            Vector3 pos = transform.position;

            if (Time.time < _fleeUntil)
            {
                _wander.Step(25f, 6f);
                return;
            }

            if (Cfg.StealEnabled.Value && GoblinState.Now >= _nview.GetZDO().GetFloat(Keys.StealCooldown, 0f))
            {
                Player mark = Player.GetClosestPlayer(pos, 30f);
                if (mark != null && !mark.IsDead())
                {
                    StalkAndSteal(mark);
                    return;
                }
            }

            if (_wander.Step(22f, 8f))
            {
                PickDestination();
            }
        }

        private void StalkAndSteal(Player mark)
        {
            Vector3 markPos = mark.transform.position;
            float distance = Vector3.Distance(transform.position, markPos);
            _wander.SetAnchor(markPos, 1.5f);
            _ai.m_randomMoveInterval = 0.5f;

            if (distance > 3.5f)
            {
                return;
            }

            Rpc.SendSteal(this, mark);
            _nview.GetZDO().Set(Keys.StealCooldown, (float)(GoblinState.Now + Cfg.StealCooldownSeconds.Value));
            Speech.Say(_nview, Lines.Pick(Lines.JoeSteal, mark.GetPlayerName()));

            // Bolt away from the victim.
            Vector3 away = transform.position - markPos;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f)
            {
                away = Random.insideUnitSphere;
                away.y = 0f;
            }
            _wander.SetDestination(transform.position + away.normalized * 60f);
            _wander.SetAnchor(transform.position + away.normalized * 20f, 4f);
            _ai.m_randomMoveInterval = 2f;
            _fleeUntil = Time.time + 20f;
        }

        private void PickDestination()
        {
            _ai.m_randomMoveInterval = 2f;
            Vector3 pos = transform.position;

            if (GoblinState.SeanKnown && Random.value < Cfg.SeanSeekChance.Value)
            {
                Vector3 seanPos = GoblinState.SeanPos;
                if (Vector3.Distance(pos, seanPos) < 3000f)
                {
                    _wander.SetDestination(seanPos);
                    return;
                }
            }

            if (_wander.TryPickLandPoint(pos, 120f, 400f, out Vector3 point))
            {
                _wander.SetDestination(point);
            }
        }

        /// <summary>A rage phrase sends Joe charging at whoever said it.</summary>
        private void UpdateRage()
        {
            Player target = Player.GetAllPlayers().FirstOrDefault(p => p.GetPlayerID() == GoblinState.RageTarget);
            Vector3 goal = target != null ? target.transform.position : GoblinState.RageOrigin;
            if (Vector3.Distance(transform.position, goal) < 400f)
            {
                _wander.SetDestination(goal);
                _wander.Step(20f, 3f);
            }
            else if (_wander.Step(22f, 8f))
            {
                // Too far to chase; stay hostile but keep roaming.
                PickDestination();
            }
        }

        private void UpdateTaunts()
        {
            if (Time.time < _nextTaunt)
            {
                return;
            }
            _nextTaunt = Time.time + Random.Range(Cfg.TauntIntervalMin.Value, Mathf.Max(Cfg.TauntIntervalMin.Value, Cfg.TauntIntervalMax.Value));

            Player nearest = Player.GetClosestPlayer(transform.position, 40f);
            if (nearest == null)
            {
                return; // Nobody to hear it.
            }

            Speech.Say(_nview, Lines.Pick(Lines.JoeAmbient, nearest.GetPlayerName()));
        }

        // ---------------------------------------------------------------- scaling

        private void UpdateScaling()
        {
            if (Time.time < _nextScale)
            {
                return;
            }
            _nextScale = Time.time + 15f;

            float level = PowerLevel.Evaluate(transform.position);
            float hpMult = Mathf.Lerp(Cfg.HealthMultMin.Value, Cfg.HealthMultMax.Value, level);
            float dmgMult = Mathf.Lerp(Cfg.DamageMultMin.Value, Cfg.DamageMultMax.Value, level);

            ZDO zdo = _nview.GetZDO();
            zdo.Set(Keys.Level, level);
            zdo.Set(Keys.DamageMult, dmgMult);

            // m_health is the prefab's base value; the live max is stored on the ZDO by SetMaxHealth.
            float newMax = Mathf.Max(10f, _character.m_health * hpMult);
            float oldMax = _character.GetMaxHealth();
            if (Mathf.Abs(newMax - oldMax) > 1f)
            {
                float ratio = oldMax > 0f ? Mathf.Clamp01(_character.GetHealth() / oldMax) : 1f;
                _character.SetMaxHealth(newMax);
                _character.SetHealth(newMax * ratio);
            }
        }

        internal static float DamageMultiplier(Character joe)
        {
            ZNetView view = joe.GetComponent<ZNetView>();
            return view != null && view.IsValid() ? view.GetZDO().GetFloat(Keys.DamageMult, 1f) : 1f;
        }

        internal static int DisplayLevel(Character joe)
        {
            ZNetView view = joe.GetComponent<ZNetView>();
            float level = view != null && view.IsValid() ? view.GetZDO().GetFloat(Keys.Level, 0f) : 0f;
            return Mathf.RoundToInt(level * 100f);
        }

        // ---------------------------------------------------------------- stolen goods

        internal void AddToStash(string prefab, int amount, int quality, int variant)
        {
            if (!_nview.IsValid() || !_nview.IsOwner())
            {
                return;
            }
            ZDO zdo = _nview.GetZDO();
            string entry = string.Join("|", prefab, amount.ToString(CultureInfo.InvariantCulture),
                quality.ToString(CultureInfo.InvariantCulture), variant.ToString(CultureInfo.InvariantCulture));
            string current = zdo.GetString(Keys.Stash, "");
            zdo.Set(Keys.Stash, current.Length == 0 ? entry : current + ";" + entry);
        }

        /// <summary>Drop everything Joe stole. Called on the owner as he dies.</summary>
        internal void DropStash()
        {
            if (!_nview.IsValid() || !_nview.IsOwner())
            {
                return;
            }

            ZDO zdo = _nview.GetZDO();
            string raw = zdo.GetString(Keys.Stash, "");
            zdo.Set(Keys.Stash, "");
            if (raw.Length == 0 || ObjectDB.instance == null)
            {
                return;
            }

            var dropped = new StringBuilder();
            foreach (string entry in raw.Split(';'))
            {
                string[] parts = entry.Split('|');
                if (parts.Length != 4)
                {
                    continue;
                }

                GameObject prefab = ObjectDB.instance.GetItemPrefab(parts[0]);
                ItemDrop template = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                if (template == null)
                {
                    continue;
                }

                ItemDrop.ItemData item = template.m_itemData.Clone();
                item.m_stack = int.Parse(parts[1], CultureInfo.InvariantCulture);
                item.m_quality = int.Parse(parts[2], CultureInfo.InvariantCulture);
                item.m_variant = int.Parse(parts[3], CultureInfo.InvariantCulture);
                item.m_durability = item.GetMaxDurability();

                Vector3 pos = transform.position + Vector3.up + Random.insideUnitSphere * 0.5f;
                ItemDrop.DropItem(item, item.m_stack, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                dropped.Append(parts[0]).Append(' ');
            }

            GoblinPackPlugin.Log.LogInfo($"Joe dropped his stash: {dropped}");
        }

        internal void SayDeathLine()
        {
            if (!_nview.IsValid() || !_nview.IsOwner())
            {
                return;
            }
            Speech.Say(_nview, Lines.Pick(Lines.JoeDeath), true);
        }
    }
}
