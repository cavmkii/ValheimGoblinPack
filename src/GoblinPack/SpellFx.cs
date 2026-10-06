using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GoblinPack
{
    /// <summary>
    /// Client-side visuals for the fight. Borrows the look of vanilla staff projectiles and Eikthyr's
    /// lightning, spawned as local-only copies with their networking and gameplay components removed,
    /// so nothing here can deal damage or create world objects. Damage is done by
    /// <see cref="Fight"/> on the authority only.
    /// </summary>
    internal static class SpellFx
    {
        internal enum Element
        {
            Fire,
            Frost,
            Lightning,
        }

        internal class Profile
        {
            public Element Element;
            public float Radius;
            public float DuelPercent;
            public float BaseDamage;
            public float Push;
            public float Speed;
            public float Arc;
            public float Scale = 1f;
            public GameObject Bolt;
            public EffectList HitEffects;
            public GameObject ImpactPrefab;
        }

        private static Profile[] _profiles;

        public static float Speed(Spell spell) => GetProfile(spell).Speed;

        /// <summary>Rebuilt lazily per game session, once ObjectDB and ZNetScene exist.</summary>
        public static void Reset()
        {
            _profiles = null;
        }

        public static Profile GetProfile(Spell spell)
        {
            if (_profiles == null)
            {
                Build();
            }
            return _profiles[(int)spell];
        }

        private static void Build()
        {
            Projectile fireball = ProjectileOf("StaffFireball");
            Projectile ice = ProjectileOf("StaffIceShards");
            GameObject lightning = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab("lightningAOE") : null;

            if (fireball == null || ice == null)
            {
                GoblinPackPlugin.Log.LogWarning("Staff projectiles not found; fight visuals will be reduced.");
            }

            _profiles = new Profile[5];
            _profiles[(int)Spell.JoeFireball] = new Profile
            {
                Element = Element.Fire, Radius = 4f, DuelPercent = 0.04f, BaseDamage = 25f, Push = 40f, Speed = 25f, Arc = 3f,
                Bolt = fireball?.gameObject, HitEffects = fireball?.m_hitEffects,
            };
            _profiles[(int)Spell.JoeMeteor] = new Profile
            {
                Element = Element.Fire, Radius = 6f, DuelPercent = 0.07f, BaseDamage = 45f, Push = 80f, Speed = 30f, Arc = 0f, Scale = 2.2f,
                Bolt = fireball?.gameObject, HitEffects = fireball?.m_hitEffects,
            };
            _profiles[(int)Spell.SeanIceShard] = new Profile
            {
                Element = Element.Frost, Radius = 3.5f, DuelPercent = 0.04f, BaseDamage = 25f, Push = 30f, Speed = 30f, Arc = 1.5f,
                Bolt = ice?.gameObject, HitEffects = ice?.m_hitEffects,
            };
            _profiles[(int)Spell.SeanLightning] = new Profile
            {
                Element = Element.Lightning, Radius = 5f, DuelPercent = 0.06f, BaseDamage = 40f, Push = 60f, Speed = 250f, Arc = 0f,
                Bolt = null, HitEffects = lightning == null ? ice?.m_hitEffects : null, ImpactPrefab = lightning,
            };
            _profiles[(int)Spell.SeanHail] = new Profile
            {
                Element = Element.Frost, Radius = 2.5f, DuelPercent = 0.015f, BaseDamage = 12f, Push = 15f, Speed = 35f, Arc = 0f, Scale = 1.5f,
                Bolt = ice?.gameObject, HitEffects = ice?.m_hitEffects,
            };
        }

        private static Projectile ProjectileOf(string itemName)
        {
            GameObject item = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(itemName) : null;
            ItemDrop drop = item != null ? item.GetComponent<ItemDrop>() : null;
            GameObject projectile = drop != null ? drop.m_itemData.m_shared.m_attack.m_attackProjectile : null;
            return projectile != null ? projectile.GetComponent<Projectile>() : null;
        }

        public static void PlayBolt(Spell spell, Vector3 from, Vector3 to, float travel)
        {
            Profile profile = GetProfile(spell);
            Vector3 dir = to - from;
            Quaternion rot = dir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(dir) : Quaternion.identity;

            GameObject bolt = profile.Bolt != null ? SpawnVisual(profile.Bolt, from, rot, profile.Scale, 10f) : new GameObject("GP_Bolt");
            bolt.AddComponent<BoltMover>().Init(from, to, travel, profile.Arc, () => PlayImpact(profile, to));
        }

        private static void PlayImpact(Profile profile, Vector3 point)
        {
            if (profile.ImpactPrefab != null)
            {
                SpawnVisual(profile.ImpactPrefab, point, Quaternion.identity, 1f, 5f);
            }

            if (profile.HitEffects?.m_effectPrefabs == null)
            {
                return;
            }
            foreach (EffectList.EffectData effect in profile.HitEffects.m_effectPrefabs)
            {
                if (effect != null && effect.m_enabled && effect.m_prefab != null)
                {
                    SpawnVisual(effect.m_prefab, point, Quaternion.identity, profile.Scale, 8f);
                }
            }
        }

        /// <summary>Instantiate a local, inert copy of a prefab: no ZDO, no physics, no damage.</summary>
        private static GameObject SpawnVisual(GameObject prefab, Vector3 pos, Quaternion rot, float scale, float lifetime)
        {
            bool previous = ZNetView.m_forceDisableInit;
            ZNetView.m_forceDisableInit = true;
            GameObject go;
            try
            {
                go = Object.Instantiate(prefab, pos, rot);
            }
            finally
            {
                ZNetView.m_forceDisableInit = previous;
            }

            try
            {
                foreach (MonoBehaviour behaviour in go.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour is Projectile || behaviour is ZSyncTransform || behaviour is Aoe || behaviour is TimedDestruction)
                    {
                        Object.DestroyImmediate(behaviour);
                    }
                }
                foreach (ZNetView view in go.GetComponentsInChildren<ZNetView>(true))
                {
                    Object.DestroyImmediate(view);
                }
                foreach (Collider collider in go.GetComponentsInChildren<Collider>(true))
                {
                    Object.DestroyImmediate(collider);
                }
                foreach (Rigidbody body in go.GetComponentsInChildren<Rigidbody>(true))
                {
                    Object.DestroyImmediate(body);
                }
            }
            catch (Exception e)
            {
                GoblinPackPlugin.Log.LogDebug($"Stripping {prefab.name}: {e.Message}");
            }

            go.transform.localScale *= scale;
            Object.Destroy(go, lifetime);
            return go;
        }
    }

    /// <summary>Flies a visual from A to B along an optional arc, then fires a callback.</summary>
    internal class BoltMover : MonoBehaviour
    {
        private Vector3 _from;
        private Vector3 _to;
        private float _duration;
        private float _arc;
        private float _elapsed;
        private Action _onArrive;

        public void Init(Vector3 from, Vector3 to, float duration, float arc, Action onArrive)
        {
            _from = from;
            _to = to;
            _duration = Mathf.Max(0.05f, duration);
            _arc = arc;
            _onArrive = onArrive;
            transform.position = from;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);
            Vector3 pos = Vector3.Lerp(_from, _to, t) + Vector3.up * (_arc * 4f * t * (1f - t));
            Vector3 delta = pos - transform.position;
            if (delta.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(delta);
            }
            transform.position = pos;

            if (t >= 1f)
            {
                try
                {
                    _onArrive?.Invoke();
                }
                finally
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}
