using UnityEngine;

namespace TowerDefense3D.Enemies
{
    /// <summary>
    /// Shows the thermal shield an enemy still has. Opacity tracks the remaining hits, so the
    /// shield reads as its own health bar, it flashes while a thermal shock is eating into it,
    /// and it switches off once the last hit lands.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyThermalShieldView : MonoBehaviour
    {
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
        private static readonly int TintColorProperty = Shader.PropertyToID("_TintColor");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");

        [SerializeField] private GameObject shieldRoot;
        [SerializeField] private Color thermalShockFlashColor = new Color(1f, 0.45f, 0.15f, 1f);
        [SerializeField, Min(0.01f)] private float flashDurationSeconds = 0.25f;
        [SerializeField, Min(1)] private int flashCycles = 3;
        [SerializeField, Min(0.01f)] private float opacityLerpSeconds = 0.15f;

        private Renderer[] renderers;
        private MaterialPropertyBlock properties;
        private Color[] authoredColors;
        private float flashRemainingSeconds;
        private float renderedOpacity;
        private bool isInitialized;

        public void Bind(EnemySnapshot enemy)
        {
            EnsureInitialized();
            flashRemainingSeconds = 0f;
            renderedOpacity = CalculateTargetOpacity(enemy);
            Apply(renderedOpacity, 0f);
            SetVisible(renderedOpacity > 0f);
        }

        public void Render(EnemySnapshot enemy, float deltaTime)
        {
            EnsureInitialized();
            if (shieldRoot == null)
            {
                return;
            }

            float target = CalculateTargetOpacity(enemy);
            renderedOpacity = Mathf.MoveTowards(
                renderedOpacity,
                target,
                deltaTime / opacityLerpSeconds);

            if (flashRemainingSeconds > 0f)
            {
                flashRemainingSeconds = Mathf.Max(0f, flashRemainingSeconds - deltaTime);
            }

            bool visible = renderedOpacity > 0.001f;
            SetVisible(visible);
            if (visible)
            {
                Apply(renderedOpacity, CalculateFlashAmount());
            }
        }

        /// <summary>
        /// Called when a thermal shock reaction lands, so the player can see that this reaction
        /// is what damages the shield.
        /// </summary>
        public void ShowThermalShockHit()
        {
            EnsureInitialized();
            flashRemainingSeconds = flashDurationSeconds;
        }

        public void Release()
        {
            EnsureInitialized();
            flashRemainingSeconds = 0f;
            renderedOpacity = 0f;
            SetVisible(false);
        }

        internal bool OwnsRenderer(Renderer candidate)
        {
            EnsureInitialized();
            for (int index = 0; index < renderers.Length; index++)
            {
                if (renderers[index] == candidate)
                {
                    return true;
                }
            }

            return false;
        }

        private float CalculateFlashAmount()
        {
            if (flashRemainingSeconds <= 0f)
            {
                return 0f;
            }

            float progress = 1f - flashRemainingSeconds / flashDurationSeconds;
            float wave = Mathf.Abs(Mathf.Sin(progress * Mathf.PI * flashCycles));
            return wave * (flashRemainingSeconds / flashDurationSeconds);
        }

        private static float CalculateTargetOpacity(EnemySnapshot enemy)
        {
            int maximumHits = enemy.Definition != null
                ? enemy.Definition.ThermalShockHitsToBreakShield
                : 0;
            if (maximumHits <= 0)
            {
                return 0f;
            }

            return Mathf.Clamp01((float)enemy.RemainingThermalShieldHits / maximumHits);
        }

        private void SetVisible(bool visible)
        {
            if (shieldRoot != null && shieldRoot.activeSelf != visible)
            {
                shieldRoot.SetActive(visible);
            }
        }

        private void Apply(float opacity, float flashAmount)
        {
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null)
                {
                    continue;
                }

                Color authored = authoredColors[index];
                Color tinted = Color.Lerp(authored, thermalShockFlashColor, flashAmount);
                tinted.a = authored.a * opacity;
                properties.Clear();
                properties.SetColor(BaseColorProperty, tinted);
                properties.SetColor(TintColorProperty, tinted);
                properties.SetColor(ColorProperty, tinted);
                renderer.SetPropertyBlock(properties);
            }
        }

        private void EnsureInitialized()
        {
            if (isInitialized)
            {
                return;
            }

            isInitialized = true;
            properties = new MaterialPropertyBlock();
            renderers = shieldRoot != null
                ? shieldRoot.GetComponentsInChildren<Renderer>(true)
                : new Renderer[0];
            authoredColors = new Color[renderers.Length];
            for (int index = 0; index < renderers.Length; index++)
            {
                Material material = renderers[index].sharedMaterial;
                authoredColors[index] = ReadAuthoredColor(material);
            }
        }

        private static Color ReadAuthoredColor(Material material)
        {
            if (material == null)
            {
                return Color.white;
            }

            if (material.HasProperty(BaseColorProperty))
            {
                return material.GetColor(BaseColorProperty);
            }

            if (material.HasProperty(TintColorProperty))
            {
                return material.GetColor(TintColorProperty);
            }

            return material.HasProperty(ColorProperty)
                ? material.GetColor(ColorProperty)
                : Color.white;
        }
    }
}
