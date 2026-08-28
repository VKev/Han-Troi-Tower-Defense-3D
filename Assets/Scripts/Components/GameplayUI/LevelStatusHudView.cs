using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class LevelStatusHudView : MonoBehaviour, ILevelStatusHudView
    {
        [SerializeField] private Text goldText;
        [SerializeField] private Text healthText;
        [SerializeField] private Image healthFill;

        public void RenderGold(int gold)
        {
            goldText.text = gold.ToString("N0");
        }

        public void RenderHealth(int currentHealth, int maximumHealth)
        {
            healthText.text = $"{currentHealth}/{maximumHealth}";
            healthFill.fillAmount = maximumHealth <= 0
                ? 0f
                : (float)currentHealth / maximumHealth;
        }
    }
}
