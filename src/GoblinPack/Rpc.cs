using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GoblinPack
{
    /// <summary>
    /// Routed (world-level) RPCs. Per-object RPCs (speech, spells) live on the NPC ZNetViews.
    /// Every payload is a ZPackage so the wire format can grow without touching Register calls.
    /// </summary>
    internal static class Rpc
    {
        public const string State = "GP_State";
        public const string Rage = "GP_Rage";
        public const string Steal = "GP_Steal";
        public const string Stash = "GP_Stash";
        public const string Summon = "GP_Summon";

        private static double _lastRageYell;

        public static void Register()
        {
            ZRoutedRpc rpc = ZRoutedRpc.instance;
            if (rpc == null)
            {
                return;
            }

            GoblinState.Reset();
            SpellFx.Reset();
            Puke.Reset();
            rpc.Register<ZPackage>(State, OnState);
            rpc.Register<ZPackage>(Rage, OnRage);
            rpc.Register<ZPackage>(Steal, OnSteal);
            rpc.Register<ZPackage>(Stash, OnStash);
            rpc.Register<ZPackage>(Summon, OnSummon);
            BabyWar.Register(rpc);
        }

        // ---- server -> everyone: positions and rage, so owners of Joe can find Sean across the map

        public static void BroadcastState(bool joeKnown, Vector3 joePos, bool seanKnown, Vector3 seanPos)
        {
            ZPackage pkg = new ZPackage();
            pkg.Write(joeKnown);
            pkg.Write(joePos);
            pkg.Write(seanKnown);
            pkg.Write(seanPos);
            pkg.Write((long)(GoblinState.RageUntil * 1000d));
            pkg.Write(GoblinState.RageTarget);
            pkg.Write(GoblinState.RageOrigin);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, State, pkg);
        }

        private static void OnState(long sender, ZPackage pkg)
        {
            GoblinState.JoeKnown = pkg.ReadBool();
            GoblinState.JoePos = pkg.ReadVector3();
            GoblinState.SeanKnown = pkg.ReadBool();
            GoblinState.SeanPos = pkg.ReadVector3();
            double rageUntil = pkg.ReadLong() / 1000d;
            long rageTarget = pkg.ReadLong();
            Vector3 rageOrigin = pkg.ReadVector3();
            if (rageUntil > GoblinState.RageUntil)
            {
                GoblinState.RageUntil = rageUntil;
                GoblinState.RageTarget = rageTarget;
                GoblinState.RageOrigin = rageOrigin;
            }
        }

        // ---- chat trigger: whoever typed the phrase tells everybody

        public static void SendRage(Player speaker)
        {
            ZPackage pkg = new ZPackage();
            pkg.Write(speaker.GetPlayerID());
            pkg.Write(speaker.transform.position);
            pkg.Write(speaker.GetPlayerName());
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, Rage, pkg);
        }

        private static void OnRage(long sender, ZPackage pkg)
        {
            long playerId = pkg.ReadLong();
            Vector3 origin = pkg.ReadVector3();
            string name = pkg.ReadString();

            GoblinState.RageUntil = GoblinState.Now + Cfg.RageSeconds.Value;
            GoblinState.RageTarget = playerId;
            GoblinState.RageOrigin = origin;
            BabyWar.NoteRage(playerId, origin);

            // Routed RPCs to Everybody can arrive twice on a listen server; only yell once.
            if (GoblinState.Now - _lastRageYell < 3d)
            {
                return;
            }
            _lastRageYell = GoblinState.Now;

            foreach (JoeBrain joe in JoeBrain.Instances)
            {
                joe.OnRage(name);
            }
        }

        // ---- theft: Joe's owner asks the victim's client to give something up

        public static void SendSteal(JoeBrain joe, Player victim)
        {
            ZNetView victimView = victim.GetComponent<ZNetView>();
            ZNetView joeView = joe.GetComponent<ZNetView>();
            if (victimView == null || !victimView.IsValid() || joeView == null || !joeView.IsValid())
            {
                return;
            }

            ZPackage pkg = new ZPackage();
            pkg.Write(joeView.GetZDO().m_uid);
            ZRoutedRpc.instance.InvokeRoutedRPC(victimView.GetZDO().GetOwner(), Steal, pkg);
        }

        private static void OnSteal(long sender, ZPackage pkg)
        {
            ZDOID joeId = pkg.ReadZDOID();
            Player player = Player.m_localPlayer;
            if (player == null || player.IsDead())
            {
                return;
            }

            Inventory inventory = player.GetInventory();
            List<ItemDrop.ItemData> candidates = inventory.GetAllItems()
                .Where(item => !item.m_equipped && item.m_dropPrefab != null)
                .ToList();

            if (candidates.Count == 0)
            {
                Notify(player, "Joe rummaged through your pockets and found nothing worth taking.");
                return;
            }

            ItemDrop.ItemData stolen = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            int amount = Mathf.Min(stolen.m_stack, UnityEngine.Random.Range(1, Mathf.Max(1, Cfg.StealMaxStack.Value) + 1));
            string prefab = stolen.m_dropPrefab.name;
            int quality = stolen.m_quality;
            int variant = stolen.m_variant;
            string displayName = Localization.instance.Localize(stolen.m_shared.m_name);

            if (!inventory.RemoveItem(stolen, amount))
            {
                return;
            }

            Notify(player, $"Joe stole {amount}x {displayName}! Kill him to get it back.");

            ZPackage reply = new ZPackage();
            reply.Write(joeId);
            reply.Write(prefab);
            reply.Write(amount);
            reply.Write(quality);
            reply.Write(variant);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, Stash, reply);
        }

        private static void OnStash(long sender, ZPackage pkg)
        {
            ZDOID joeId = pkg.ReadZDOID();
            string prefab = pkg.ReadString();
            int amount = pkg.ReadInt();
            int quality = pkg.ReadInt();
            int variant = pkg.ReadInt();

            GameObject instance = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(joeId) : null;
            if (instance != null && instance.TryGetComponent(out JoeBrain joe))
            {
                joe.AddToStash(prefab, amount, quality, variant);
            }
        }

        // ---- admin summon (console command) handled by the server

        public static void SendSummon(string who, Vector3 position)
        {
            ZPackage pkg = new ZPackage();
            pkg.Write(who);
            pkg.Write(position);
            // The overload without a target peer goes to the server. (GetServerPeerID is private in the game.)
            ZRoutedRpc.instance.InvokeRoutedRPC(Summon, pkg);
        }

        private static void OnSummon(long sender, ZPackage pkg)
        {
            if (!ZNet.instance.IsServer())
            {
                return;
            }
            string who = pkg.ReadString();
            Vector3 position = pkg.ReadVector3();
            if (!IsAdminOrHost(sender))
            {
                GoblinPackPlugin.Log.LogWarning($"Ignoring summon from non-admin peer {sender}");
                return;
            }
            WorldDirector.Summon(who, position);
        }

        internal static bool IsAdminOrHost(long sender)
        {
            if (sender == ZDOMan.GetSessionID())
            {
                return true;
            }
            try
            {
                return IsAdminPeer(sender);
            }
            catch (Exception e)
            {
                // Admin API differs between game versions; fail closed.
                GoblinPackPlugin.Log.LogWarning($"Admin check unavailable: {e.Message}");
                return false;
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static bool IsAdminPeer(long sender)
        {
            ZNetPeer peer = ZNet.instance.GetPeer(sender);
            return peer != null && ZNet.instance.IsAdmin(peer.m_socket.GetHostName());
        }

        public static void Notify(Player player, string message)
        {
            player.Message(MessageHud.MessageType.Center, message);
        }
    }
}
