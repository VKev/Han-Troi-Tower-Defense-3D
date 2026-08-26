using UnityEngine;

namespace TowerDefense3D.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemyDamageFlashView : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField, Min(0.01f)] private float damageFlashDurationSeconds = 0.1f;
        [SerializeField] private Color fullHealthDamageColor = Color.white;
        [SerializeField] private Color lowHealthDamageColor = Color.red;

        private Renderer[] renderers;
        private MaterialPropertyBlock properties;
        private float renderedHealth;
        private float damageFlashRemainingSeconds;

        private void Awake()
        {
            EnsureInitialized();
        }

        public void Bind(EnemySnapshot enemy)
        {
            EnsureInitialized();
            renderedHealth = enemy.Health;
            damageFlashRemainingSeconds = 0f;
            Clear();
        }

        public void Render(EnemySnapshot enemy, float deltaTime)
        {
            EnsureInitialized();

            if (enemy.Health < renderedHealth)
            {
                float preDamageHealthFraction = Mathf.Clamp01(renderedHealth / enemy.Definition.BaseMaxHealth);
                ApplyColor(Color.Lerp(
                    lowHealthDamageColor,
                    fullHealthDamageColor,
                    preDamageHealthFraction));
                damageFlashRemainingSeconds = damageFlashDurationSeconds;
                renderedHealth = enemy.Health;
                return;
            }

            renderedHealth = enemy.Health;
            if (damageFlashRemainingSeconds > 0f)
            {
                damageFlashRemainingSeconds = Mathf.Max(
                    0f,
                    damageFlashRemainingSeconds - deltaTime);
                if (damageFlashRemainingSeconds == 0f)
                {
                    Clear();
                }
            }
        }

        public void Release()
        {
            EnsureInitialized();
            renderedHealth = 0f;
            damageFlashRemainingSeconds = 0f;
            Clear();
        }

        private void ApplyColor(Color color)
        {
            properties.SetColor(BaseColor, color);
            for (int index = 0; index < renderers.Length; index++)
            {
                renderers[index].SetPropertyBlock(properties);
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
        }
    }
}
