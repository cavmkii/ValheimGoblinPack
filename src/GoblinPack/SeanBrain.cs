using System.Collections.Generic;
using UnityEngine;

namespace GoblinPack
{
    /// <summary>
    /// Sean travels between camp spots, stops whenever customers are nearby, and holds his ground
    /// when Joe picks a fight. Trading itself is the vanilla Trader component on the same object.
    /// </summary>
    public class SeanBrain : MonoBehaviour
    {
        internal static readonly List<SeanBrain> Instances = new List<SeanBrain>();
        private static readonly Dictionary<Character, SeanBrain> ByCharacter = new Dictionary<Character, SeanBrain>();

        private const Heightmap.Biome Forbidden =
            Heightmap.Biome.AshLands | Heightmap.Biome.DeepNorth | Heightmap.Biome.Ocean | Heightmap.Biome.Mistlands;

        private Character _character;
        private BaseAI _ai;
        private ZNetView _nview;
        private Wander _wander;
        private DeathWatch _deathWatch;

        private float _nextThink;
        private float _campUntil;
        private float _nextPitch;
        private readonly Dictionary<long, float> _greeted = new Dictionary<long, float>();

        internal Character Character => _character;
        internal ZNetView NView => _nview;

        public static bool TryGet(Character character, out SeanBrain sean)
        {
            sean = null;
            return character != null && ByCharacter.TryGetValue(character, out sean);
        }

        public static SeanBrain FindNear(Vector3 position, float range)
        {
            SeanBrain best = null;
            float bestDistance = range;
            foreach (SeanBrain sean in Instances)
            {
                if (sean._character == null || sean._character.IsDead())
                {
                    continue;
                }
                float d = Vector3.Distance(sean.transform.position, position);
                if (d <= bestDistance)
                {
                    best = sean;
                    bestDistance = d;
                }
            }
            return best;
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

            _wander = new Wander(_ai, _nview, Forbidden);
            _deathWatch = new DeathWatch(_character, _nview);
            Speech.Register(_nview, 2.1f);
            Instances.Add(this);
            ByCharacter[_character] = this;
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

            Vector3 pos = transform.position;

            if (IsInFight())
            {
                _wander.SetAnchor(pos, 4f);
                return;
            }

            if (BabyWar.Active)
            {
                // Hold position behind his Security and shout at everyone.
                _wander.ClearDestination();
                _wander.SetAnchor(pos, 3f);
                if (Time.time >= _nextPitch && Player.GetClosestPlayer(pos, 60f) != null)
                {
                    _nextPitch = Time.time + Random.Range(12f, 22f);
                    Speech.Say(_nview, Lines.Pick(Lines.SeanWar), true);
                }
                return;
            }

            Player customer = Player.GetClosestPlayer(pos, 15f);
            if (customer != null)
            {
                // Open for business: stand still while anyone is browsing.
                _wander.ClearDestination();
                _wander.SetAnchor(pos, 1.5f);
                _campUntil = Mathf.Max(_campUntil, Time.time + 30f);
                Greet(customer);
                Pitch();
                return;
            }

            if (Time.time < _campUntil)
            {
                return;
            }

            if (_wander.Step(20f, 5f))
            {
                if (_wander.HasDestination)
                {
                    return;
                }

                // Arrived (or gave up): camp for a while, then move on.
                if (Time.time >= _campUntil && _campUntil > 0f)
                {
                    _campUntil = 0f;
                    if (_wander.TryPickLandPoint(pos, 250f, 700f, out Vector3 next))
                    {
                        _wander.SetDestination(next);
                    }
                }
                else
                {
                    _campUntil = Time.time + Random.Range(180f, 360f);
                    _wander.SetAnchor(pos, 3f);
                }
            }
        }

        private bool IsInFight()
        {
            foreach (JoeBrain joe in JoeBrain.Instances)
            {
                if (Vector3.Distance(joe.transform.position, transform.position) < Cfg.FightTriggerRange.Value * 2.5f
                    && Fight.IsActive(joe.NView))
                {
                    return true;
                }
            }
            return false;
        }

        private void Greet(Player customer)
        {
            long id = customer.GetPlayerID();
            if (_greeted.TryGetValue(id, out float last) && Time.time - last < 180f)
            {
                return;
            }
            _greeted[id] = Time.time;
            Speech.Say(_nview, Lines.Pick(Lines.SeanGreet, customer.GetPlayerName()));
            _nextPitch = Time.time + Random.Range(25f, 45f);
        }

        private void Pitch()
        {
            if (Time.time < _nextPitch)
            {
                return;
            }
            _nextPitch = Time.time + Random.Range(30f, 60f);
            Speech.Say(_nview, Lines.Pick(Lines.SeanPitch));
        }
    }
}
