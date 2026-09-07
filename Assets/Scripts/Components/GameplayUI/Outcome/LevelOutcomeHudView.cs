using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class LevelOutcomeHudView : MonoBehaviour, ILevelOutcomeHudView
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text titleText;

        [Header("Score")]
        [Tooltip("The three star slots, left to right. Each is filled or hollow, never hidden.")]
        [SerializeField] private Image[] starSlots = Array.Empty<Image>();

        [Tooltip("Drawn in a slot the run earned.")]
        [SerializeField] private Sprite earnedStar;

        [Tooltip("Drawn in a slot the run did not earn, so the score reads out of three.")]
        [SerializeField] private Sprite unearnedStar;

        [Tooltip("The burst behind the stars. Hidden on a defeat, which has nothing to celebrate.")]
        [SerializeField] private GameObject starBurst;

        [Header("Rows")]
        [Tooltip("The green bar. Driven by its rect rather than by fillAmount, so the sliced end caps keep their radius - the same bargain the frog's HUD bar makes.")]
        [SerializeField] private RectTransform healthFill;

        [SerializeField] private TMP_Text healthValueText;
        [SerializeField] private TMP_Text goldValueText;

        [Header("Buttons")]
        [SerializeField] private Button playAgainButton;
        [SerializeField] private Button nextLevelButton;
        [SerializeField] private Button returnToLevelMenuButton;

        private bool isInitialized;

        public event Action PlayAgainRequested;
        public event Action NextLevelRequested;
        public event Action ReturnToLevelMenuRequested;

        public void Initialize()
        {
            if (isInitialized)
            {
                return;
            }

            playAgainButton.onClick.AddListener(HandlePlayAgainClicked);
            nextLevelButton.onClick.AddListener(HandleNextLevelClicked);
            returnToLevelMenuButton.onClick.AddListener(HandleReturnToLevelMenuClicked);
            isInitialized = true;
        }

        public void Render(LevelOutcomeHudState state)
        {
            root.SetActive(state.IsVisible);

            if (titleText != null)
            {
                titleText.text = state.TitleText;
            }

            RenderStars(state);
            RenderHealth(state);

            if (goldValueText != null)
            {
                goldValueText.text = state.Gold.ToString("N0");
            }

            // Hidden rather than disabled so the button row re-flows around what is left.
            nextLevelButton.gameObject.SetActive(state.NextLevelVisible);
        }

        private void RenderStars(LevelOutcomeHudState state)
        {
            for (int index = 0; index < starSlots.Length; index++)
            {
                if (starSlots[index] == null)
                {
                    continue;
                }

                starSlots[index].sprite = index < state.Stars ? earnedStar : unearnedStar;
            }

            if (starBurst != null)
            {
                starBurst.SetActive(state.Outcome == LevelOutcome.Victory);
            }
        }

        /// <summary>
        /// Stretches the bar from its left edge, exactly as the frog's own HUD bar does.
        /// </summary>
        /// <remarks>
        /// The right anchor moves and the left one stays at zero, rather than setting
        /// <c>Image.fillAmount</c>: a filled image ignores its sprite's border, which would
        /// flatten the rounded caps this bar is drawn with.
        /// </remarks>
        private void RenderHealth(LevelOutcomeHudState state)
        {
            if (healthFill != null)
            {
                healthFill.anchorMin = new Vector2(0f, healthFill.anchorMin.y);
                healthFill.anchorMax = new Vector2(state.HealthRatio, healthFill.anchorMax.y);
            }

            if (healthValueText != null)
            {
                healthValueText.text = $"{state.CurrentHealth}/{state.MaximumHealth}";
            }
        }

        public void Shutdown()
        {
            if (!isInitialized)
            {
                return;
            }

            playAgainButton.onClick.RemoveListener(HandlePlayAgainClicked);
            nextLevelButton.onClick.RemoveListener(HandleNextLevelClicked);
            returnToLevelMenuButton.onClick.RemoveListener(HandleReturnToLevelMenuClicked);
            isInitialized = false;
        }

        private void HandlePlayAgainClicked()
        {
            PlayAgainRequested?.Invoke();
        }

        private void HandleNextLevelClicked()
        {
            NextLevelRequested?.Invoke();
        }

        private void HandleReturnToLevelMenuClicked()
        {
            ReturnToLevelMenuRequested?.Invoke();
        }
    }
}
