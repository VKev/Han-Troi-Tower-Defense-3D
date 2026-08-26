using TowerDefense3D.Towers;
using UnityEngine;

namespace TowerDefense3D.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemyElementStatusView : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Color fireColor = new Color(1f, 0.25f, 0.05f);
        [SerializeField] private Color waterColor = new Color(0.1f, 0.55f, 1f);
        [SerializeField] private Color windColor = new Color(0.2f, 1f, 0.45f);
        [SerializeField] private Color earthColor = new Color(0.8f, 0.55f, 0.15f);
        [SerializeField] private Color reactionCooldownColor = Color.white;

        private Renderer[] renderers;
        private MaterialPropertyBlock properties;
        private EnemyElementPhase renderedPhase;
        private ElementType renderedElement;

        private void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            properties = new MaterialPropertyBlock();
            renderedPhase = EnemyElementPhase.Ready;
        }

        public void Render(EnemyElementState state)
        {
            EnsureInitialized();
            if (state.Phase == renderedPhase
                && (state.Phase != EnemyElementPhase.Marked
                    || state.Element == renderedElement))
            {
                return;
            }

            renderedPhase = state.Phase;
            renderedElement = state.Element;
            if (state.Phase == EnemyElementPhase.Ready)
            {
                Clear();
                return;
            }

            Color color = state.Phase == EnemyElementPhase.ReactionCooldown
                ? reactionCooldownColor
                : GetElementColor(state.Element);
            properties.SetColor(BaseColor, color);
            for (int index = 0; index < renderers.Length; index++)
            {
                renderers[index].SetPropertyBlock(properties);
            }
        }

        public void Release()
        {
            EnsureInitialized();
            renderedPhase = EnemyElementPhase.Ready;
            renderedElement = default;
            Clear();
        }

        private Color GetElementColor(ElementType element)
        {
            switch (element)
            {
                case ElementType.Fire:
                    return fireColor;
                case ElementType.Water:
                    return waterColor;
                case ElementType.Wind:
                    return windColor;
                case ElementType.Earth:
                    return earthColor;
                default:
                    return Color.white;
            }
        }

        private void Clear()
        {
            for (int index = 0; index < renderers.Length; index++)
            {
                renderers[index].SetPropertyBlock(null);
            }
        }

        private void EnsureInitialized()
        {
            if (renderers != null)
            {
                return;
            }

            renderers = GetComponentsInChildren<Renderer>(true);
            properties = new MaterialPropertyBlock();
            renderedPhase = EnemyElementPhase.Ready;
        }
    }
}
