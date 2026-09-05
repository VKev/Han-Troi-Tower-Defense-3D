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

            healthFill.fillAmount = maximumHealth <= 0
                ? 0f
                : (float)currentHealth / maximumHealth;
        }
    }
}
