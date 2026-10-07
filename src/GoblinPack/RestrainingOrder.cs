using UnityEngine;

namespace GoblinPack
{
    /// <summary>
    /// Whether a player is wearing the Restraining Order. Equipment isn't synced between clients,
    /// so each client publishes its own player's status on the player's ZDO, where whoever owns Joe
    /// can read it.
    /// </summary>
    internal static class RestrainingOrder
    {
        public const string PrefabName = "GP_RestrainingOrder";
        private const string Key = "gp_restraining";

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

            bool wearing = false;
            foreach (ItemDrop.ItemData item in player.GetInventory().GetEquippedItems())
            {
                if (item.m_dropPrefab != null && item.m_dropPrefab.name == PrefabName)
                {
                    wearing = true;
                    break;
                }
            }

            ZDO zdo = view.GetZDO();
            if (zdo.GetBool(Key, false) != wearing)
            {
                zdo.Set(Key, wearing);
            }
        }

        public static bool IsProtected(Player player)
        {
            ZNetView view = player != null ? player.GetComponent<ZNetView>() : null;
            return view != null && view.IsValid() && view.GetZDO().GetBool(Key, false);
        }
    }
}
