using UnityEngine;

namespace GoblinPack
{
    /// <summary>Items whose effect depends on other clients knowing you're wearing them.</summary>
    internal enum Worn
    {
        RestrainingOrder,
        BabyBonnet,
        Merch,
    }

    /// <summary>
    /// Equipment isn't synced between clients, so each client publishes what its own player is
    /// wearing on the player's ZDO, where whoever owns Joe, the babies or Sean's Security can read it.
    /// </summary>
    internal static class WornItems
    {
        public const string RestrainingOrderPrefab = "GP_RestrainingOrder";
        public const string BabyBonnetPrefab = "GP_BabyBonnet";
        public const string MerchPrefab = "GP_Merch";

        private static readonly string[] Prefabs = { RestrainingOrderPrefab, BabyBonnetPrefab, MerchPrefab };
        private static readonly string[] Keys = { "gp_restraining", "gp_bonnet", "gp_merch" };

        private static float _nextCheck;

        public static void TickLocalPlayer()
        {
            if (Time.time < _nextCheck)
            {
                return;
            }
            _nextCheck = Time.time + 1f;

            Player player = Player.m_localPlayer;
            ZNetView view = player != null ? player.GetComponent<ZNetView>() : null;
            if (view == null || !view.IsValid() || !view.IsOwner())
            {
                return;
            }

            var wearing = new bool[Prefabs.Length];
            foreach (ItemDrop.ItemData item in player.GetInventory().GetEquippedItems())
            {
                string name = item.m_dropPrefab != null ? item.m_dropPrefab.name : null;
                for (int i = 0; i < Prefabs.Length; i++)
                {
                    if (name == Prefabs[i])
                    {
                        wearing[i] = true;
                    }
                }
            }

            ZDO zdo = view.GetZDO();
            for (int i = 0; i < Prefabs.Length; i++)
            {
                if (zdo.GetBool(Keys[i], false) != wearing[i])
                {
                    zdo.Set(Keys[i], wearing[i]);
                }
            }
        }

        public static bool Has(Player player, Worn item)
        {
            ZNetView view = player != null ? player.GetComponent<ZNetView>() : null;
            return view != null && view.IsValid() && view.GetZDO().GetBool(Keys[(int)item], false);
        }
    }
}
