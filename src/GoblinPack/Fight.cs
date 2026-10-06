using System.Collections;
using UnityEngine;

namespace GoblinPack
{
    internal enum Spell
    {
        JoeFireball = 0,
        JoeMeteor = 1,
        SeanIceShard = 2,
        SeanLightning = 3,
        SeanHail = 4,
    }

    /// <summary>
    /// Joe vs Sean. Joe's owner is the single authority: it picks spells, broadcasts them so every
    /// client draws the same bolt, then applies the area damage itself when each lands.
    /// </summary>
    internal class Fight
    {
        private const string SpellRpc = "GP_Spell";

        /// <summary>Set while spell damage is applied, so Joe's melee damage multiplier isn't stacked on top.</summary>
        internal static bool ApplyingSpell;

        private readonly JoeBrain _joe;
        private float _nextCast;
        private bool _joeTurn;

        public Fight(JoeBrain joe)
        {
            _joe = joe;
        }

        public void RegisterRpcs()
        {
            _joe.NView.Register<ZPackage>(SpellRpc, OnSpell);
        }

        private ZDO Zdo => _joe.NView.GetZDO();

        public static bool IsActive(ZNetView joeView)
        {
            return joeView != null && joeView.IsValid() && joeView.GetZDO().GetFloat(Keys.FightEnd, 0f) > GoblinState.Now;
        }

        public void TryStart()
        {
            if (GoblinState.Now < Zdo.GetFloat(Keys.FightCooldown, 0f))
            {
                return;
            }

            SeanBrain sean = SeanBrain.FindNear(_joe.transform.position, Cfg.FightTriggerRange.Value);
            if (sean == null)
            {
                return;
            }

            Zdo.Set(Keys.FightEnd, (float)(GoblinState.Now + Cfg.FightSeconds.Value));
            _nextCast = Time.time + 2.5f;
            _joe.Wander.ClearDestination();

            Speech.Say(_joe.NView, Lines.Pick(Lines.JoeFightStart), true);
            Speech.Say(sean.NView, Lines.Pick(Lines.SeanFightStart), true);
            Speech.Announce(_joe.NView, "Joe has found Sean. Take cover!");
        }

        /// <summary>Returns true while a fight is running (Joe's other behaviour is suspended).</summary>
        public bool Update()
        {
            float end = Zdo.GetFloat(Keys.FightEnd, 0f);
            if (end <= 0f)
            {
                return false;
            }

            SeanBrain sean = SeanBrain.FindNear(_joe.transform.position, Cfg.FightTriggerRange.Value * 2.5f);
            if (GoblinState.Now >= end || sean == null || sean.Character.IsDead())
            {
                Finish(sean);
                return false;
            }

            // Hold the arena: both stay roughly in place and face off.
            _joe.Wander.SetAnchor(_joe.transform.position, 4f);

            if (Time.time >= _nextCast)
            {
                _nextCast = Time.time + Random.Range(0.9f, 2.2f);
                CastRound(sean);
            }
            return true;
        }

        private void Finish(SeanBrain sean)
        {
            Zdo.Set(Keys.FightEnd, 0f);
            Zdo.Set(Keys.FightCooldown, (float)(GoblinState.Now + Cfg.FightCooldownMinutes.Value * 60f));

            Speech.Say(_joe.NView, Lines.Pick(Lines.JoeFightEnd), true);
            if (sean != null && !sean.Character.IsDead())
            {
                Speech.Say(sean.NView, Lines.Pick(Lines.SeanFightEnd), true);
            }

            // Joe storms off somewhere far away.
            if (_joe.Wander.TryPickLandPoint(_joe.transform.position, 250f, 500f, out Vector3 away))
            {
                _joe.Wander.SetDestination(away);
            }
        }

        private void CastRound(SeanBrain sean)
        {
            _joe.StartCoroutine(_joeTurn ? Cast(_joe.Character, sean.Character, JoeSpell()) : Cast(sean.Character, _joe.Character, SeanSpell()));
            // Mostly alternate, sometimes someone gets a double turn.
            if (Random.value < 0.8f)
            {
                _joeTurn = !_joeTurn;
            }
        }

        private static Spell JoeSpell() => Random.value < 0.65f ? Spell.JoeFireball : Spell.JoeMeteor;

        private static Spell SeanSpell()
        {
            float roll = Random.value;
            return roll < 0.45f ? Spell.SeanIceShard : roll < 0.8f ? Spell.SeanLightning : Spell.SeanHail;
        }

        private IEnumerator Cast(Character caster, Character target, Spell spell)
        {
            int bolts = spell == Spell.SeanHail ? 4 : 1;
            for (int i = 0; i < bolts; i++)
            {
                if (caster == null || target == null)
                {
                    yield break;
                }

                Vector3 aim = target.transform.position;
                // Plenty of misses, so the blasts spread around the arena and catch bystanders.
                float missChance = spell == Spell.SeanHail ? 0.7f : 0.35f;
                if (Random.value < missChance)
                {
                    Vector2 offset = Random.insideUnitCircle.normalized * Random.Range(4f, 12f);
                    aim += new Vector3(offset.x, 0f, offset.y);
                    aim.y = GroundHeight(aim);
                }

                Vector3 from = SpellOrigin(spell, caster, aim);
                float travel = Mathf.Clamp(Vector3.Distance(from, aim) / SpellFx.Speed(spell), 0.2f, 3f);

                ZPackage pkg = new ZPackage();
                pkg.Write((int)spell);
                pkg.Write(from);
                pkg.Write(aim);
                pkg.Write(travel);
                _joe.NView.InvokeRPC(ZNetView.Everybody, SpellRpc, pkg);

                _joe.StartCoroutine(Impact(caster, spell, aim, travel));
                yield return new WaitForSeconds(0.25f);
            }
        }

        private static Vector3 SpellOrigin(Spell spell, Character caster, Vector3 aim)
        {
            switch (spell)
            {
                case Spell.JoeMeteor:
                    return aim + new Vector3(Random.Range(-15f, 15f), 40f, Random.Range(-15f, 15f));
                case Spell.SeanLightning:
                    return aim + Vector3.up * 45f;
                case Spell.SeanHail:
                    return aim + new Vector3(Random.Range(-4f, 4f), 30f, Random.Range(-4f, 4f));
                default:
                    return caster.transform.position + Vector3.up * 1.6f + caster.transform.forward * 0.6f;
            }
        }

        private static void OnSpell(long sender, ZPackage pkg)
        {
            Spell spell = (Spell)pkg.ReadInt();
            Vector3 from = pkg.ReadVector3();
            Vector3 to = pkg.ReadVector3();
            float travel = pkg.ReadSingle();
            SpellFx.PlayBolt(spell, from, to, travel);
        }

        private IEnumerator Impact(Character caster, Spell spell, Vector3 point, float delay)
        {
            yield return new WaitForSeconds(delay);

            SpellFx.Profile profile = SpellFx.GetProfile(spell);

            float level = PowerLevel.Evaluate(point);
            foreach (Character victim in Character.GetAllCharacters())
            {
                if (victim == null || victim == caster || victim.IsDead())
                {
                    continue;
                }
                float distance = Vector3.Distance(victim.transform.position, point);
                if (distance > profile.Radius)
                {
                    continue;
                }

                bool duelist = JoeBrain.TryGet(victim, out _) || SeanBrain.TryGet(victim, out _);
                float damage;
                if (duelist)
                {
                    // Scaled to the duelists' own health so fights last; Patches clamps them above 15%.
                    damage = victim.GetMaxHealth() * profile.DuelPercent;
                }
                else
                {
                    damage = (profile.BaseDamage + profile.BaseDamage * 3f * level) * Cfg.BystanderDamageMultiplier.Value;
                }
                damage *= Mathf.Lerp(1f, 0.4f, distance / profile.Radius);
                if (damage <= 0f)
                {
                    continue;
                }

                HitData hit = new HitData();
                hit.m_point = victim.transform.position;
                hit.m_dir = (victim.transform.position - point).normalized;
                hit.m_pushForce = profile.Push;
                switch (profile.Element)
                {
                    case SpellFx.Element.Fire:
                        hit.m_damage.m_fire = damage * 0.7f;
                        hit.m_damage.m_blunt = damage * 0.3f;
                        break;
                    case SpellFx.Element.Frost:
                        hit.m_damage.m_frost = damage * 0.7f;
                        hit.m_damage.m_pierce = damage * 0.3f;
                        break;
                    default:
                        hit.m_damage.m_lightning = damage;
                        break;
                }
                hit.SetAttacker(caster);
                ApplyingSpell = true;
                try
                {
                    victim.Damage(hit);
                }
                finally
                {
                    ApplyingSpell = false;
                }
            }
        }

        private static float GroundHeight(Vector3 p)
        {
            return ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(p, out float height) ? height : p.y;
        }
    }
}
