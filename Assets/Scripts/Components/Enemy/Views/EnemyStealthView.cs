using UnityEngine;

namespace TowerDefense3D.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemyStealthView : MonoBehaviour
    {
        private static readonly int StealthAlpha = Shader.PropertyToID("_StealthAlpha");

        [SerializeField] private Renderer bodyRenderer;
        [SerializeField, Range(0f, 1f)] private float hiddenAlpha = 0.16f;
        [SerializeField, Min(0.01f)] private float transitionDurationSeconds = 0.25f;

        private MaterialPropertyBlock properties;
        private float displayedAlpha = 1f;

        public void Bind(EnemySnapshot enemy)
        {
            displayedAlpha = GetTargetAlpha(enemy);
            Apply();
        }

        public void Render(EnemySnapshot enemy, float deltaTime)
        {
            float targetAlpha = GetTargetAlpha(enemy);
            displayedAlpha = Mathf.MoveTowards(
                displayedAlpha,
                targetAlpha,
                deltaTime / transitionDurationSeconds);
            Apply();
        }

        public void Release()
        {
            displayedAlpha = 1f;
            Apply();
        }

        private float GetTargetAlpha(EnemySnapshot enemy)
        {
            return enemy.Definition is StealthEnemyDefinition && enemy.IsHidden
                ? hiddenAlpha
                : 1f;
        }

        private void Apply()
        {
            if (bodyRenderer == null)
            {
                return;
            }

            if (properties == null)
            {
                properties = new MaterialPropertyBlock();
            }

            bodyRenderer.GetPropertyBlock(properties);
            properties.SetFloat(StealthAlpha, displayedAlpha);
            bodyRenderer.SetPropertyBlock(properties);
        }
    }
}
