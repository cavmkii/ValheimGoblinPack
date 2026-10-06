using UnityEngine;

namespace GoblinPack
{
    /// <summary>Floating NPC speech, synced through the NPC's own ZNetView so everyone nearby sees the same line.</summary>
    internal static class Speech
    {
        private const string SayRpc = "GP_Say";
        private const string AnnounceRpc = "GP_Announce";

        public static void Register(ZNetView nview, float height)
        {
            GameObject go = nview.gameObject;
            nview.Register<string, bool>(SayRpc, (sender, text, large) => Show(go, height, text, large));
            nview.Register<string>(AnnounceRpc, (sender, text) => ShowAnnouncement(go.transform.position, text));
        }

        public static void Say(ZNetView nview, string text, bool large = false)
        {
            if (nview != null && nview.IsValid())
            {
                nview.InvokeRPC(ZNetView.Everybody, SayRpc, text, large);
            }
        }

        /// <summary>Centre-screen message for players within earshot.</summary>
        public static void Announce(ZNetView nview, string text)
        {
            if (nview != null && nview.IsValid())
            {
                nview.InvokeRPC(ZNetView.Everybody, AnnounceRpc, text);
            }
        }

        private static void Show(GameObject talker, float height, string text, bool large)
        {
            if (Chat.instance == null || talker == null)
            {
                return;
            }
            Chat.instance.SetNpcText(talker, Vector3.up * height, 40f, large ? 8f : 6f, "", text, large);
        }

        private static void ShowAnnouncement(Vector3 origin, string text)
        {
            Player player = Player.m_localPlayer;
            if (player != null && Vector3.Distance(player.transform.position, origin) < 150f)
            {
                Rpc.Notify(player, text);
            }
        }
    }
}
