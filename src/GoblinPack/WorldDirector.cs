using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GoblinPack
{
    /// <summary>
    /// Server-only. Keeps exactly one Joe and one Sean in the world: remembers their ZDO ids per
    /// world, notices when one has died (its ZDO is gone), and respawns it near a random player
    /// after the configured delay. Also broadcasts their positions so Joe can hunt Sean.
    /// </summary>
    internal static class WorldDirector
    {
        private class Tracked
        {
            public string Key;
            public string Prefab;
            public Func<bool> Enabled;
            public Func<float> RespawnMinutes;
            public Heightmap.Biome Forbidden;

            public ZDOID Id = ZDOID.None;
            public double DiedAt = -1d;
            public bool Alive;
            public Vector3 LastPos;
        }

        private static readonly Tracked Joe = new Tracked
        {
            Key = "joe",
            Prefab = Content.JoePrefab,
            Enabled = () => Cfg.JoeEnabled.Value,
            RespawnMinutes = () => Cfg.JoeRespawnMinutes.Value,
            Forbidden = Heightmap.Biome.AshLands | Heightmap.Biome.DeepNorth | Heightmap.Biome.Ocean,
        };

        private static readonly Tracked Sean = new Tracked
        {
            Key = "sean",
            Prefab = Content.SeanPrefab,
            Enabled = () => Cfg.SeanEnabled.Value,
            RespawnMinutes = () => Cfg.SeanRespawnMinutes.Value,
            Forbidden = Heightmap.Biome.AshLands | Heightmap.Biome.DeepNorth | Heightmap.Biome.Ocean | Heightmap.Biome.Mistlands,
        };

        private static readonly Tracked[] All = { Joe, Sean };

        private static long _loadedWorld;
        private static float _nextTick;
        private static float _graceUntil;
        private static float _nextBroadcast;

        public static void Tick()
        {
            if (!ZNet.instance.IsServer() || ZDOMan.instance == null || ZRoutedRpc.instance == null)
            {
                return;
            }

            EnsureLoaded();

            if (Time.time >= _nextTick && Time.time >= _graceUntil)
            {
                _nextTick = Time.time + 5f;
                foreach (Tracked t in All)
                {
                    Check(t);
                }
            }

            if (Time.time >= _nextBroadcast)
            {
                _nextBroadcast = Time.time + 10f;
                Rpc.BroadcastState(Joe.Alive, Joe.LastPos, Sean.Alive, Sean.LastPos);
            }
        }

        private static void Check(Tracked t)
        {
            ZDO zdo = t.Id.IsNone() ? null : ZDOMan.instance.GetZDO(t.Id);
            if (zdo != null)
            {
                t.Alive = true;
                t.LastPos = zdo.GetPosition();
                return;
            }

            if (t.Alive || !t.Id.IsNone())
            {
                // It existed and now its ZDO is gone: killed (or removed by an admin).
                t.Alive = false;
                t.Id = ZDOID.None;
                t.DiedAt = GoblinState.Now;
                Save();
                GoblinPackPlugin.Log.LogInfo($"{t.Key} is gone; respawning in {t.RespawnMinutes()} minutes.");
            }

            if (!t.Enabled())
            {
                return;
            }
            if (t.DiedAt > 0d && GoblinState.Now - t.DiedAt < t.RespawnMinutes() * 60d)
            {
                return;
            }

            List<Vector3> players = PlayerPositions();
            if (players.Count == 0)
            {
                return;
            }

            Vector3 near = players[UnityEngine.Random.Range(0, players.Count)];
            if (Wander.TryPickLandPoint(near, Cfg.SpawnDistanceMin.Value, Cfg.SpawnDistanceMax.Value, t.Forbidden, out Vector3 point))
            {
                Spawn(t, SnapToGround(point));
            }
        }

        private static void Spawn(Tracked t, Vector3 position)
        {
            GameObject prefab = ZNetScene.instance.GetPrefab(t.Prefab);
            if (prefab == null)
            {
                GoblinPackPlugin.Log.LogError($"Prefab {t.Prefab} is not registered.");
                return;
            }

            GameObject go = Object.Instantiate(prefab, position, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
            ZNetView view = go.GetComponent<ZNetView>();
            t.Id = view.GetZDO().m_uid;
            t.Alive = true;
            t.LastPos = position;
            t.DiedAt = -1d;
            Save();
            GoblinPackPlugin.Log.LogInfo($"Spawned {t.Key} at {position}");
        }

        /// <summary>Admin summon: replace the existing one with a fresh one at the given spot.</summary>
        public static void Summon(string who, Vector3 position)
        {
            EnsureLoaded();
            Tracked t = string.Equals(who, "sean", StringComparison.OrdinalIgnoreCase) ? Sean : Joe;

            ZDO existing = t.Id.IsNone() ? null : ZDOMan.instance.GetZDO(t.Id);
            if (existing != null)
            {
                existing.SetOwner(ZDOMan.GetSessionID());
                ZDOMan.instance.DestroyZDO(existing);
            }
            Spawn(t, position);
        }

        public static string Describe()
        {
            return $"Joe: {(GoblinState.JoeKnown ? GoblinState.JoePos.ToString("F0") : "unknown/dead")}, " +
                   $"Sean: {(GoblinState.SeanKnown ? GoblinState.SeanPos.ToString("F0") : "unknown/dead")}, " +
                   $"rage: {(GoblinState.IsRaging ? (GoblinState.RageUntil - GoblinState.Now).ToString("F0") + "s" : "no")}";
        }

        private static List<Vector3> PlayerPositions()
        {
            var positions = new List<Vector3>();
            foreach (ZNetPeer peer in ZNet.instance.GetPeers())
            {
                if (peer.IsReady())
                {
                    positions.Add(peer.m_refPos);
                }
            }
            if (Player.m_localPlayer != null)
            {
                positions.Add(Player.m_localPlayer.transform.position);
            }
            return positions;
        }

        /// <summary>
        /// On a dedicated server the spawn zone is usually not loaded, so we rely on the world
        /// generator height (ignores player terrain edits). Lift a bit to avoid spawning underground.
        /// </summary>
        private static Vector3 SnapToGround(Vector3 p)
        {
            if (ZoneSystem.instance.IsZoneLoaded(p) && ZoneSystem.instance.GetGroundHeight(p, out float height))
            {
                p.y = height;
            }
            p.y += 1.5f;
            return p;
        }

        // ------------------------------------------------------------------ persistence

        private static string StatePath => Path.Combine(Path.Combine(Paths.ConfigPath, "GoblinPack"),
            ZNet.instance.GetWorldUID().ToString(CultureInfo.InvariantCulture) + ".txt");

        private static void EnsureLoaded()
        {
            long world = ZNet.instance.GetWorldUID();
            if (world == _loadedWorld)
            {
                return;
            }
            _loadedWorld = world;
            // Give the world a moment to finish loading before deciding anyone is dead.
            _graceUntil = Time.time + 30f;

            foreach (Tracked t in All)
            {
                t.Id = ZDOID.None;
                t.DiedAt = -1d;
                t.Alive = false;
            }

            try
            {
                if (!File.Exists(StatePath))
                {
                    return;
                }
                foreach (string line in File.ReadAllLines(StatePath))
                {
                    // key=userId:id:diedAt
                    string[] kv = line.Split('=');
                    if (kv.Length != 2)
                    {
                        continue;
                    }
                    Tracked t = Array.Find(All, x => x.Key == kv[0].Trim());
                    string[] parts = kv[1].Split(':');
                    if (t == null || parts.Length != 3)
                    {
                        continue;
                    }
                    long user = long.Parse(parts[0], CultureInfo.InvariantCulture);
                    uint id = uint.Parse(parts[1], CultureInfo.InvariantCulture);
                    t.Id = user == 0 && id == 0 ? ZDOID.None : new ZDOID(user, id);
                    t.DiedAt = double.Parse(parts[2], CultureInfo.InvariantCulture);
                }
            }
            catch (Exception e)
            {
                GoblinPackPlugin.Log.LogWarning($"Could not read GoblinPack state: {e.Message}");
            }
        }

        private static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(StatePath));
                var lines = new List<string>();
                foreach (Tracked t in All)
                {
                    long user = t.Id.IsNone() ? 0 : t.Id.UserID;
                    uint id = t.Id.IsNone() ? 0 : t.Id.ID;
                    lines.Add($"{t.Key}={user.ToString(CultureInfo.InvariantCulture)}:{id.ToString(CultureInfo.InvariantCulture)}:{t.DiedAt.ToString("R", CultureInfo.InvariantCulture)}");
                }
                File.WriteAllLines(StatePath, lines.ToArray());
            }
            catch (Exception e)
            {
                GoblinPackPlugin.Log.LogWarning($"Could not save GoblinPack state: {e.Message}");
            }
        }
    }
}
