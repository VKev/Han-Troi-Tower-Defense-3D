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

            // The bar stretches from its left edge: anchorMin stays at 0 and only the right
            // anchor moves, which keeps the sliced end caps at their authored radius.
            RectTransform fill = healthFill.rectTransform;
            fill.anchorMax = new Vector2(ratio, 1f);
        }
    }
}
