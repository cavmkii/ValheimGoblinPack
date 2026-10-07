using System.Collections.Generic;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GoblinPack
{
    /// <summary>
    /// Joe's war cauldron. Use an item on it from the hotbar (like an offering bowl) to curse his
    /// next wave of babies. The item is consumed; the server applies the curse to the next wave.
    /// </summary>
    public class WarCauldron : MonoBehaviour, Hoverable, Interactable
    {
        private static readonly Dictionary<string, Curse> Offerings = new Dictionary<string, Curse>
        {
            ["Pukeberries"] = Curse.Puke,
            ["Thistle"] = Curse.Slow,
            ["TrollHide"] = Curse.Heavy,
        };

        public string GetHoverName() => "Joe's War Cauldron";

        public string GetHoverText()
        {
            return "Joe's War Cauldron\n" +
                   "Use an item on it (hotbar key while looking at it) to curse the next wave:\n" +
                   "Pukeberries: they puke  |  Thistle: they're slow  |  Troll hide: heavy and weak";
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (!hold && user is Player player)
            {
                Rpc.Notify(player, "Offer Pukeberries, Thistle or Troll hide to curse Joe's next wave.");
            }
            return false;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item)
        {
            if (!(user is Player player) || item == null)
            {
                return false;
            }
            string prefab = item.m_dropPrefab != null ? item.m_dropPrefab.name : "";
            if (!Offerings.TryGetValue(prefab, out Curse curse))
            {
                Rpc.Notify(player, "The cauldron spits that back out. Try Pukeberries, Thistle or Troll hide.");
                return true;
            }
            if (!BabyWar.Active)
            {
                Rpc.Notify(player, "The cauldron is cold. There's no war on.");
                return true;
            }

            player.GetInventory().RemoveOneItem(item);
            BabyWar.SendCurse(curse, player.GetPlayerName());
            return true;
        }

        // ------------------------------------------------------------------ prefabs

        /// <summary>
        /// A networked, save-able prop that only has another prefab's looks: its meshes are copied, its
        /// components (cooking stations, build pieces, destructibles) are not. Used for the camp.
        /// </summary>
        internal static GameObject MakeProp(string name, string lookalike, bool interactable, Color? glow)
        {
            GameObject source = PrefabManager.Instance.GetPrefab(lookalike);
            if (source == null)
            {
                GoblinPackPlugin.Log.LogWarning($"Baby Wars: {lookalike} not found; skipping {name}.");
                return null;
            }

            GameObject prefab = PrefabManager.Instance.CreateEmptyPrefab(name, true);
            Object.DestroyImmediate(prefab.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(prefab.GetComponent<MeshFilter>());
            BoxCollider box = prefab.GetComponent<BoxCollider>();

            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool any = false;
            foreach (MeshFilter filter in source.GetComponentsInChildren<MeshFilter>(true))
            {
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                if (filter.sharedMesh == null || renderer == null)
                {
                    continue;
                }
                var part = new GameObject(filter.name);
                part.transform.SetParent(prefab.transform, false);
                part.transform.localPosition = source.transform.InverseTransformPoint(filter.transform.position);
                part.transform.localRotation = Quaternion.Inverse(source.transform.rotation) * filter.transform.rotation;
                part.transform.localScale = filter.transform.lossyScale;
                part.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                part.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;

                Bounds local = filter.sharedMesh.bounds;
                Vector3 center = part.transform.localPosition + part.transform.localRotation * Vector3.Scale(local.center, part.transform.localScale);
                Vector3 size = Vector3.Scale(local.size, part.transform.localScale);
                var partBounds = new Bounds(center, new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)));
                if (!any)
                {
                    bounds = partBounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(partBounds);
                }
            }

            if (box != null)
            {
                box.center = any ? bounds.center : Vector3.up * 0.5f;
                box.size = any ? bounds.size : Vector3.one;
            }
            if (glow.HasValue)
            {
                Light light = prefab.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = glow.Value;
                light.range = 8f;
                light.intensity = 1.5f;
            }
            if (interactable)
            {
                prefab.AddComponent<WarCauldron>();
            }

            int pieceLayer = LayerMask.NameToLayer("piece");
            if (pieceLayer >= 0)
            {
                prefab.layer = pieceLayer;
            }

            PrefabManager.Instance.AddPrefab(new CustomPrefab(prefab, false));
            return prefab;
        }
    }
}
