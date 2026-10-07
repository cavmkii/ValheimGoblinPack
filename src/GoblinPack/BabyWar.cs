using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GoblinPack
{
    /// <summary>Wave curses, offered at Joe's war cauldron. Stored as a bitmask on each baby's ZDO.</summary>
    [Flags]
    internal enum Curse
    {
        None = 0,
        Puke = 1,
        Slow = 2,
        Heavy = 4,
    }

    /// <summary>
    /// The Baby Wars. Saying the rage phrase enough times starts a war near whoever said it last:
    /// Joe sets up a war camp and sends waves of tiny Fulings at the players, while Sean shows up with
    /// his Security. The server runs the war; clients only get its state and announcements.
    /// </summary>
    internal static class BabyWar
    {
        public const string Commando = "GP_BabyCommando";
        public const string Grenadier = "GP_BabyGrenadier";
        public const string Berserker = "GP_BabyBerserker";
        public const string Security = "GP_SeanSecurity";
        public const string Cauldron = "GP_WarCauldron";
        public const string Banner = "GP_WarBanner";
        public const string Totem = "GP_WarTotem";

        private static readonly string[] WarPrefabs = { Commando, Grenadier, Berserker, Security, Cauldron, Banner, Totem };

        private const string StateRpc = "GP_WarState";
        private const string EventRpc = "GP_WarEvent";
        private const string CurseRpc = "GP_WarCurse";
        private const string BonnetRpc = "GP_WarBonnet";
        private const string CommandRpc = "GP_WarCommand";

        private enum EventKind
        {
            Start = 0,
            Wave = 1,
            End = 2,
            Notice = 3,
        }

        // ---- state every client knows (sent by the server)
        public static bool Active { get; private set; }
        public static Vector3 Camp { get; private set; }

        // ---- server-only state
        private static readonly List<double> Triggers = new List<double>();
        private static readonly HashSet<long> Speakers = new HashSet<long>();
        private static readonly List<ZDOID> Babies = new List<ZDOID>();
        private static long _lastTriggerPlayer;
        private static double _lastTriggerTime;
        private static double _endsAt;
        private static double _cooldownUntil;
        private static double _nextWaveAt;
        private static int _wave;
        private static int _lastWaveSize;
        private static Curse _pendingCurses;
        private static float _firstTick = -1f;
        private static bool _cleaned;
        private static float _nextTick;
        private static float _nextState;
        private static bool _forcedStorm;

        public static void Register(ZRoutedRpc rpc)
        {
            Active = false;
            Triggers.Clear();
            Speakers.Clear();
            Babies.Clear();
            _firstTick = -1f;
            _cleaned = false;
            _wave = 0;
            _pendingCurses = Curse.None;
            _cooldownUntil = 0d;

            rpc.Register<ZPackage>(StateRpc, OnState);
            rpc.Register<ZPackage>(EventRpc, OnEvent);
            rpc.Register<ZPackage>(CurseRpc, OnCurse);
            rpc.Register<ZPackage>(BonnetRpc, OnBonnet);
            rpc.Register<ZPackage>(CommandRpc, OnCommand);
        }

        // ================================================================== trigger (server)

        /// <summary>Called on every client when anyone says the rage phrase; only the server acts.</summary>
        public static void NoteRage(long playerId, Vector3 origin)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || !Cfg.WarEnabled.Value)
            {
                return;
            }

            double now = GoblinState.Now;
            // The same message can arrive twice on a listen server; count it once.
            if (playerId == _lastTriggerPlayer && now - _lastTriggerTime < 2d)
            {
                return;
            }
            _lastTriggerPlayer = playerId;
            _lastTriggerTime = now;

            Speakers.Add(playerId);
            if (Active)
            {
                GiveBonnets(new[] { playerId });
                return;
            }
            if (now < _cooldownUntil)
            {
                return;
            }

            Triggers.Add(now);
            Triggers.RemoveAll(t => now - t > Cfg.WarTriggerWindowMinutes.Value * 60d);
            if (Triggers.Count >= Math.Max(1, Cfg.WarTriggerCount.Value))
            {
                Triggers.Clear();
                Start(origin);
            }
        }

        // ================================================================== lifecycle (server)

        private static void Start(Vector3 origin)
        {
            const Heightmap.Biome bad = Heightmap.Biome.Ocean | Heightmap.Biome.DeepNorth | Heightmap.Biome.AshLands;
            if (!Wander.TryPickLandPoint(origin, 25f, 40f, bad, out Vector3 camp))
            {
                camp = origin + Vector3.forward * 25f;
            }
            camp = WorldDirector.SnapToGround(camp);

            double now = GoblinState.Now;
            Active = true;
            Camp = camp;
            _wave = 0;
            _lastWaveSize = 0;
            _pendingCurses = Curse.None;
            _nextWaveAt = now + 15d;
            _endsAt = now + Cfg.WarMaxMinutes.Value * 60d;
            Babies.Clear();

            // The camp.
            Spawn(Cauldron, camp, 0f);
            for (int i = 0; i < 3; i++)
            {
                float angle = i * 120f + 30f;
                Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 6f;
                Spawn(i == 0 ? Totem : Banner, WorldDirector.SnapToGround(camp + offset), angle);
            }

            // Joe takes command; Sean arrives on the far side with his Security.
            if (Cfg.JoeEnabled.Value)
            {
                WorldDirector.Summon("joe", WorldDirector.SnapToGround(camp + Vector3.right * 3f));
            }
            if (Cfg.SeanEnabled.Value)
            {
                Vector3 away = (origin - camp);
                away.y = 0f;
                away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.back;
                Vector3 seanPos = WorldDirector.SnapToGround(camp + away * 35f);
                WorldDirector.Summon("sean", seanPos);
                for (int i = 0; i < Cfg.WarSecurityCount.Value; i++)
                {
                    Vector2 ring = UnityEngine.Random.insideUnitCircle.normalized * 4f;
                    Spawn(Security, WorldDirector.SnapToGround(seanPos + new Vector3(ring.x, 0f, ring.y)), 0f);
                }
            }

            GiveBonnets(Speakers.ToArray());
            SendEvent(EventKind.Start, "THE BABY WARS HAVE BEGUN");
            BroadcastState();
            GoblinPackPlugin.Log.LogInfo($"Baby Wars started; camp at {camp.ToString("F0")}.");
        }

        private static void End(string message)
        {
            Active = false;
            _cooldownUntil = GoblinState.Now + Cfg.WarCooldownMinutes.Value * 60d;
            int removed = RemoveWarObjects();
            Babies.Clear();
            SendEvent(EventKind.End, message);
            BroadcastState();
            GoblinPackPlugin.Log.LogInfo($"Baby Wars ended ({message}); removed {removed} war objects.");
        }

        public static void Tick()
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || ZDOMan.instance == null || ZRoutedRpc.instance == null)
            {
                return;
            }
            if (Time.time < _nextTick)
            {
                return;
            }
            _nextTick = Time.time + 2f;
            if (_firstTick < 0f)
            {
                _firstTick = Time.time;
            }

            // Anything left over from a war interrupted by a shutdown gets cleared once the world has loaded.
            if (!_cleaned && !Active && Time.time - _firstTick > 30f)
            {
                _cleaned = true;
                int removed = RemoveWarObjects();
                if (removed > 0)
                {
                    GoblinPackPlugin.Log.LogInfo($"Removed {removed} leftover Baby Wars objects.");
                }
            }

            if (Active)
            {
                double now = GoblinState.Now;
                Babies.RemoveAll(id => ZDOMan.instance.GetZDO(id) == null);

                if (Cfg.JoeEnabled.Value && !WorldDirector.JoeAlive && now - (_endsAt - Cfg.WarMaxMinutes.Value * 60d) > 20d)
                {
                    End("Joe has fallen! The babies scatter.");
                }
                else if (now > _endsAt)
                {
                    End("The Baby Wars end in a stalemate. Joe declares victory anyway.");
                }
                else if (_wave < Cfg.WarWaves.Value)
                {
                    bool mostlyDead = _wave > 0 && Babies.Count <= Mathf.CeilToInt(_lastWaveSize * 0.25f);
                    if (now >= _nextWaveAt || mostlyDead)
                    {
                        SpawnWave();
                    }
                }
                else if (Babies.Count == 0)
                {
                    End("The last baby has fallen. The Baby Wars are over... for now.");
                }
            }

            if (Time.time >= _nextState)
            {
                _nextState = Time.time + 5f;
                BroadcastState();
            }
        }

        private static void SpawnWave()
        {
            _wave++;
            int size = Math.Max(1, Cfg.WarWaveBaseSize.Value + Cfg.WarWaveGrowth.Value * (_wave - 1));
            _lastWaveSize = size;
            Curse curses = _pendingCurses;
            _pendingCurses = Curse.None;

            for (int i = 0; i < size; i++)
            {
                float roll = UnityEngine.Random.value;
                string prefab = _wave >= 2 && roll < 0.15f ? Berserker : roll < 0.4f ? Grenadier : Commando;
                Vector2 ring = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(4f, 10f);
                Vector3 pos = WorldDirector.SnapToGround(Camp + new Vector3(ring.x, 0f, ring.y));
                ZDO zdo = Spawn(prefab, pos, UnityEngine.Random.Range(0f, 360f));
                if (zdo != null)
                {
                    zdo.Set(BabyBrain.CurseKey, (int)curses);
                    Babies.Add(zdo.m_uid);
                }
            }

            _nextWaveAt = GoblinState.Now + Cfg.WarWaveIntervalSeconds.Value;
            string cursed = curses == Curse.None ? "" : $" (cursed: {Describe(curses)})";
            SendEvent(EventKind.Wave, $"Wave {_wave}/{Cfg.WarWaves.Value}: {size} babies{cursed}");
        }

        private static ZDO Spawn(string prefabName, Vector3 position, float yaw)
        {
            GameObject prefab = ZNetScene.instance.GetPrefab(prefabName);
            if (prefab == null)
            {
                GoblinPackPlugin.Log.LogWarning($"Baby Wars: prefab {prefabName} isn't registered.");
                return null;
            }
            GameObject go = Object.Instantiate(prefab, position, Quaternion.Euler(0f, yaw, 0f));
            ZNetView view = go.GetComponent<ZNetView>();
            return view != null && view.IsValid() ? view.GetZDO() : null;
        }

        /// <summary>Deletes every baby, guard and camp piece in the world.</summary>
        private static int RemoveWarObjects()
        {
            var hashes = new HashSet<int>(WarPrefabs.Select(name => name.GetStableHashCode()));
            int removed = 0;
            foreach (List<ZDO> list in WorldDirector.ScanWorld(hashes).Values)
            {
                foreach (ZDO zdo in list)
                {
                    zdo.SetOwner(ZDOMan.GetSessionID());
                    ZDOMan.instance.DestroyZDO(zdo);
                    removed++;
                }
            }
            return removed;
        }

        private static void GiveBonnets(long[] playerIds)
        {
            if (playerIds.Length == 0)
            {
                return;
            }
            ZPackage pkg = new ZPackage();
            pkg.Write(playerIds.Length);
            foreach (long id in playerIds)
            {
                pkg.Write(id);
            }
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, BonnetRpc, pkg);
        }

        public static string Describe(Curse curses)
        {
            var parts = new List<string>();
            if ((curses & Curse.Puke) != 0) parts.Add("Puke");
            if ((curses & Curse.Slow) != 0) parts.Add("Slow");
            if ((curses & Curse.Heavy) != 0) parts.Add("Heavy");
            return string.Join(", ", parts.ToArray());
        }

        // ================================================================== networking

        private static void BroadcastState()
        {
            ZPackage pkg = new ZPackage();
            pkg.Write(Active);
            pkg.Write(Camp);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, StateRpc, pkg);
        }

        private static void SendEvent(EventKind kind, string message)
        {
            ZPackage pkg = new ZPackage();
            pkg.Write((int)kind);
            pkg.Write(message);
            pkg.Write(Camp);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, EventRpc, pkg);
        }

        private static void OnState(long sender, ZPackage pkg)
        {
            bool active = pkg.ReadBool();
            Camp = pkg.ReadVector3();
            if (active != Active)
            {
                Active = active;
                SetStorm(active);
            }
        }

        private static void OnEvent(long sender, ZPackage pkg)
        {
            var kind = (EventKind)pkg.ReadInt();
            string message = pkg.ReadString();
            Camp = pkg.ReadVector3();

            Player player = Player.m_localPlayer;
            bool near = player != null && Vector3.Distance(player.transform.position, Camp) < 200f;
            switch (kind)
            {
                case EventKind.Start:
                    Active = true;
                    SetStorm(true);
                    if (player != null)
                    {
                        Rpc.Notify(player, message);
                    }
                    foreach (JoeBrain joe in JoeBrain.Instances)
                    {
                        joe.OnWarEvent(Lines.JoeWarStart);
                    }
                    break;
                case EventKind.Wave:
                    if (near)
                    {
                        Rpc.Notify(player, message);
                    }
                    foreach (JoeBrain joe in JoeBrain.Instances)
                    {
                        joe.OnWarEvent(Lines.JoeWarWave);
                    }
                    break;
                case EventKind.End:
                    Active = false;
                    SetStorm(false);
                    if (player != null)
                    {
                        Rpc.Notify(player, message);
                    }
                    foreach (JoeBrain joe in JoeBrain.Instances)
                    {
                        joe.OnWarEvent(Lines.JoeWarEnd);
                    }
                    break;
                default:
                    if (near)
                    {
                        Rpc.Notify(player, message);
                    }
                    break;
            }
        }

        private static void SetStorm(bool on)
        {
            if (EnvMan.instance == null)
            {
                return;
            }
            if (on)
            {
                EnvMan.instance.SetForceEnvironment("ThunderStorm");
                _forcedStorm = true;
            }
            else if (_forcedStorm)
            {
                EnvMan.instance.SetForceEnvironment("");
                _forcedStorm = false;
            }
        }

        // ---- curses: a client offers an item at the cauldron, the server queues it for the next wave

        public static void SendCurse(Curse curse, string playerName)
        {
            ZPackage pkg = new ZPackage();
            pkg.Write((int)curse);
            pkg.Write(playerName);
            ZRoutedRpc.instance.InvokeRoutedRPC(CurseRpc, pkg);
        }

        private static void OnCurse(long sender, ZPackage pkg)
        {
            if (!ZNet.instance.IsServer() || !Active)
            {
                return;
            }
            var curse = (Curse)pkg.ReadInt();
            string who = pkg.ReadString();
            _pendingCurses |= curse;
            SendEvent(EventKind.Notice, $"{who} cursed Joe's next wave: {Describe(curse)}!");
        }

        // ---- bonnets: Joe gives one to everyone who has said the phrase

        private static void OnBonnet(long sender, ZPackage pkg)
        {
            int count = pkg.ReadInt();
            var ids = new HashSet<long>();
            for (int i = 0; i < count; i++)
            {
                ids.Add(pkg.ReadLong());
            }

            Player player = Player.m_localPlayer;
            if (player == null || !ids.Contains(player.GetPlayerID()))
            {
                return;
            }
            Inventory inventory = player.GetInventory();
            if (inventory.GetAllItems().Any(item => item.m_dropPrefab != null && item.m_dropPrefab.name == WornItems.BabyBonnetPrefab))
            {
                return;
            }
            GameObject bonnet = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(WornItems.BabyBonnetPrefab) : null;
            if (bonnet != null && inventory.AddItem(bonnet, 1))
            {
                Rpc.Notify(player, "Joe slipped a Baby Bonnet into your pack. Wear it and the babies are on your side.");
            }
        }

        // ---- admin console command

        public static void SendCommand(string command, Vector3 position)
        {
            ZPackage pkg = new ZPackage();
            pkg.Write(command);
            pkg.Write(position);
            ZRoutedRpc.instance.InvokeRoutedRPC(CommandRpc, pkg);
        }

        private static void OnCommand(long sender, ZPackage pkg)
        {
            if (!ZNet.instance.IsServer())
            {
                return;
            }
            string command = pkg.ReadString();
            Vector3 position = pkg.ReadVector3();
            if (!Rpc.IsAdminOrHost(sender))
            {
                GoblinPackPlugin.Log.LogWarning($"Ignoring Baby Wars command from non-admin peer {sender}");
                return;
            }
            if (command == "start" && !Active)
            {
                Start(position);
            }
            else if (command == "stop" && Active)
            {
                End("An admin called off the Baby Wars.");
            }
        }
    }
}
