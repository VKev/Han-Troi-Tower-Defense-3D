using UnityEngine;

namespace TowerDefense3D.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemyDamageFlashView : MonoBehaviour
    {
        private static readonly int DamageFlashColor = Shader.PropertyToID("_DamageFlashColor");
        private static readonly int DamageFlashAmount = Shader.PropertyToID("_DamageFlashAmount");

        [SerializeField, Min(0.01f)] private float damageFlashDurationSeconds = 0.18f;
        [SerializeField] private Color fullHealthDamageColor = Color.white;
        [SerializeField] private Color lowHealthDamageColor = Color.red;

        private Renderer[] renderers;
        private MaterialPropertyBlock properties;
        private Color renderedDamageFlashColor;
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
                renderedDamageFlashColor = Color.Lerp(
                    lowHealthDamageColor,
                    fullHealthDamageColor,
                    preDamageHealthFraction);
                ApplyFlash(renderedDamageFlashColor, 1f);
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
                else
                {
                    ApplyFlash(
                        renderedDamageFlashColor,
                        damageFlashRemainingSeconds / damageFlashDurationSeconds);
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

        private void ApplyFlash(Color color, float amount)
        {
            properties.SetColor(DamageFlashColor, color);
            properties.SetFloat(DamageFlashAmount, amount);
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
