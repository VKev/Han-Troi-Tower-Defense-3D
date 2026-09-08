using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class LevelStatusHudView : MonoBehaviour, ILevelStatusHudView
    {
        [SerializeField] private Text goldText;

        [Tooltip("Optional. The frog's bar carries the health by its length, so the numbers are left off unless a design asks for them; wire a Text here and they come back.")]
        [SerializeField] private Text healthText;

        [Tooltip("The green bar. It is a 9-sliced capsule stretched from its left edge, so its length is driven by the rect rather than by Image.fillAmount - a filled image ignores sprite borders and would flatten the rounded end caps.")]
        [SerializeField] private Image healthFill;

        [Tooltip("Optional shake target for damage feedback. Defaults to this HUD root.")]
        [SerializeField] private RectTransform healthShakeTarget;

        [SerializeField, Min(0f)] private float healthTweenDuration = 0.32f;
        [SerializeField, Min(0f)] private float healthShakeDuration = 0.24f;
        [SerializeField, Min(0f)] private float healthShakeStrength = 5f;

        private Tween healthFillTween;
        private Tween healthShakeTween;
        private bool hasRenderedHealth;

        public void RenderGold(int gold)
        {
            goldText.text = gold.ToString("N0");
        }

        public void RenderHealth(int currentHealth, int maximumHealth)
        {
            if (healthText != null)
            {
                healthText.text = $"{currentHealth}/{maximumHealth}";
            }

            float ratio = maximumHealth <= 0
                ? 0f
                : Mathf.Clamp01((float)currentHealth / maximumHealth);

            if (healthFill == null)
            {
                return;
            }

            // The bar stretches from its left edge: anchorMin stays at 0 and only the right
            // anchor moves, which keeps the sliced end caps at their authored radius.
            RectTransform fill = healthFill.rectTransform;
            Vector2 targetAnchorMax = new Vector2(ratio, 1f);
            healthFillTween?.Kill();
            if (!hasRenderedHealth || healthTweenDuration <= 0f)
            {
                fill.anchorMax = targetAnchorMax;
            }
            else
            {
                healthFillTween = DOTween.To(
                        () => fill.anchorMax,
                        value => fill.anchorMax = value,
                        targetAnchorMax,
                        healthTweenDuration)
                    .SetEase(Ease.OutCubic)
                    .SetTarget(this);
            }

            if (hasRenderedHealth && currentHealth < maximumHealth)
            {
                RectTransform shakeTarget = healthShakeTarget != null
                    ? healthShakeTarget
                    : transform as RectTransform;
                if (shakeTarget != null && healthShakeDuration > 0f && healthShakeStrength > 0f)
                {
                    healthShakeTween?.Kill();
                    healthShakeTween = shakeTarget
                        .DOShakePosition(
                            healthShakeDuration,
                            new Vector2(healthShakeStrength, healthShakeStrength * 0.45f),
                            10,
                            80f,
                            false,
                            true)
                        .SetTarget(this);
                }
            }

            hasRenderedHealth = true;
        }

        private void OnDisable()
        {
            healthFillTween?.Kill();
            healthShakeTween?.Kill();
            healthFillTween = null;
            healthShakeTween = null;
        }
    }
}
