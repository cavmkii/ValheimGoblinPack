using UnityEngine;

namespace GoblinPack
{
    /// <summary>
    /// Joe's vomiting. Pukeberries have no body animation in Valheim: what you see is the "Puke" status
    /// effect's start effects (particles and sound from the head). We play that same effect list on Joe,
    /// on every client, through his ZNetView.
    /// </summary>
    internal static class Puke
    {
        private const string PukeRpc = "GP_Puke";
        private const float Duration = 4f;

        private static EffectList _effects;
        private static bool _looked;

        public static void Register(ZNetView nview, Character character)
        {
            nview.Register<int>(PukeRpc, (sender, unused) => Play(character));
        }

        public static void Trigger(ZNetView nview)
        {
            if (nview != null && nview.IsValid())
            {
                nview.InvokeRPC(ZNetView.Everybody, PukeRpc, 0);
            }
        }

        public static float Seconds => Duration;

        private static EffectList Effects()
        {
            if (!_looked && ObjectDB.instance != null)
            {
                _looked = true;
                GameObject berries = ObjectDB.instance.GetItemPrefab("Pukeberries");
                StatusEffect se = berries != null ? berries.GetComponent<ItemDrop>()?.m_itemData.m_shared.m_consumeStatusEffect : null;
                _effects = se != null ? se.m_startEffects : null;
                if (_effects == null || !_effects.HasEffects())
                {
                    GoblinPackPlugin.Log.LogWarning("Pukeberries effect not found; Joe can't vomit.");
                }
            }
            return _effects;
        }

        /// <summary>Forget cached effects when a new game session starts.</summary>
        public static void Reset()
        {
            _looked = false;
            _effects = null;
        }

        private static void Play(Character character)
        {
            if (character == null)
            {
                return;
            }
            EffectList effects = Effects();
            if (effects == null || !effects.HasEffects())
            {
                return;
            }

            Transform root = character.transform;
            Vector3 mouth = (character.m_head != null ? character.m_head.position : root.position + Vector3.up * 1.2f)
                            + root.forward * 0.15f;
            GameObject[] spawned = effects.Create(mouth, root.rotation, root, root.localScale.x);
            if (spawned == null)
            {
                return;
            }
            foreach (GameObject go in spawned)
            {
                if (go != null)
                {
                    Object.Destroy(go, Duration);
                }
            }
        }
    }
}
