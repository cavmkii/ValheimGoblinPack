using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace GoblinPack
{
    /// <summary>
    /// Drives long-distance travel without replacing vanilla AI. BaseAI already wanders randomly
    /// around its spawn point; we keep sliding that spawn point ("anchor") toward a far destination,
    /// so the creature drifts across the map using its own pathfinding and animations, and still
    /// fights normally whenever its AI picks a target.
    /// </summary>
    internal class Wander
    {
        private static readonly FieldInfo SpawnPointField = AccessTools.Field(typeof(BaseAI), "m_spawnPoint");
        private static bool _warned;

        private const float WorldRadius = 9500f;

        private readonly BaseAI _ai;
        private readonly ZNetView _nview;
        private readonly Heightmap.Biome _forbiddenBiomes;

        public Vector3 Destination { get; private set; }
        public bool HasDestination { get; private set; }

        private Vector3 _anchor;
        private Vector3 _savedAnchor = new Vector3(float.MaxValue, 0f, 0f);
        private Vector3 _progressPos;
        private float _progressTime;

        public Wander(BaseAI ai, ZNetView nview, Heightmap.Biome forbiddenBiomes)
        {
            _ai = ai;
            _nview = nview;
            _forbiddenBiomes = forbiddenBiomes;
        }

        public static bool Supported => SpawnPointField != null;

        /// <summary>Pin the wander anchor. Range is how far the AI may roam around it.</summary>
        public void SetAnchor(Vector3 point, float range)
        {
            _anchor = point;
            _ai.m_randomMoveRange = range;
            if (SpawnPointField == null)
            {
                if (!_warned)
                {
                    _warned = true;
                    GoblinPackPlugin.Log.LogWarning("BaseAI.m_spawnPoint not found; Joe and Sean will only roam locally.");
                }
                return;
            }

            SpawnPointField.SetValue(_ai, point);
            // Persist occasionally (only matters if ownership moves); avoids a ZDO update every tick.
            if (_nview.IsValid() && Vector3.Distance(point, _savedAnchor) > 5f)
            {
                _savedAnchor = point;
                _nview.GetZDO().Set(Keys.SpawnPoint, point);
            }
        }

        public void SetDestination(Vector3 destination)
        {
            Destination = destination;
            HasDestination = true;
            _progressPos = _ai.transform.position;
            _progressTime = Time.time;
        }

        public void ClearDestination()
        {
            HasDestination = false;
        }

        /// <summary>
        /// Advance toward the destination. Returns true when arrived (or gave up because stuck),
        /// which clears the destination.
        /// </summary>
        public bool Step(float stride, float roam)
        {
            if (!HasDestination)
            {
                return true;
            }

            Vector3 pos = _ai.transform.position;
            Vector3 flat = Destination - pos;
            flat.y = 0f;

            if (flat.magnitude < 12f)
            {
                HasDestination = false;
                return true;
            }

            if (Vector3.Distance(pos, _progressPos) > 15f)
            {
                _progressPos = pos;
                _progressTime = Time.time;
            }
            else if (Time.time - _progressTime > 75f)
            {
                // Probably blocked by water or a cliff. Give up and let the caller pick again.
                HasDestination = false;
                return true;
            }

            Vector3 toAnchor = _anchor - pos;
            toAnchor.y = 0f;
            if (toAnchor.magnitude < roam + 4f || Vector3.Dot(toAnchor, flat) < 0f)
            {
                Vector3 next = pos + flat.normalized * Mathf.Min(stride, flat.magnitude);
                SetAnchor(next, roam);
            }
            return false;
        }

        /// <summary>Random point on land in an allowed biome, between min and max metres from origin.</summary>
        public bool TryPickLandPoint(Vector3 origin, float min, float max, out Vector3 point)
        {
            return TryPickLandPoint(origin, min, max, _forbiddenBiomes, out point);
        }

        public static bool TryPickLandPoint(Vector3 origin, float min, float max, Heightmap.Biome forbidden, out Vector3 point)
        {
            WorldGenerator world = WorldGenerator.instance;
            for (int i = 0; i < 40; i++)
            {
                float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float dist = UnityEngine.Random.Range(min, max);
                Vector3 p = origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * dist;
                if (new Vector2(p.x, p.z).magnitude > WorldRadius)
                {
                    continue;
                }

                if (world != null)
                {
                    float h = world.GetHeight(p.x, p.z);
                    if (h < ZoneSystem.instance.m_waterLevel + 1.5f)
                    {
                        continue;
                    }
                    if ((world.GetBiome(p.x, p.z) & forbidden) != 0)
                    {
                        continue;
                    }
                    p.y = h;
                }

                point = p;
                return true;
            }

            point = origin;
            return false;
        }
    }

    /// <summary>Reflection helpers for fields whose names are less stable across game versions.</summary>
    internal static class R
    {
        public static bool Set(object target, string field, object value)
        {
            if (target == null)
            {
                return false;
            }
            FieldInfo info = AccessTools.Field(target.GetType(), field);
            if (info == null)
            {
                GoblinPackPlugin.Log.LogWarning($"Field {target.GetType().Name}.{field} not found; skipping.");
                return false;
            }
            try
            {
                info.SetValue(target, value);
                return true;
            }
            catch (Exception e)
            {
                GoblinPackPlugin.Log.LogWarning($"Could not set {target.GetType().Name}.{field}: {e.Message}");
                return false;
            }
        }

        public static bool SetEnum(object target, string field, string valueName)
        {
            FieldInfo info = target != null ? AccessTools.Field(target.GetType(), field) : null;
            if (info == null || !info.FieldType.IsEnum || !Enum.IsDefined(info.FieldType, valueName))
            {
                GoblinPackPlugin.Log.LogWarning($"Enum field {target?.GetType().Name}.{field}={valueName} not available; skipping.");
                return false;
            }
            info.SetValue(target, Enum.Parse(info.FieldType, valueName));
            return true;
        }

        public static object Get(object target, string field)
        {
            if (target == null)
            {
                return null;
            }
            FieldInfo info = AccessTools.Field(target.GetType(), field);
            return info?.GetValue(target);
        }
    }
}
