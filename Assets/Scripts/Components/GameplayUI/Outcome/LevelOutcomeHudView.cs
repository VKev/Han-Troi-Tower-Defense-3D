using System;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class LevelOutcomeHudView : MonoBehaviour, ILevelOutcomeHudView
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Text titleText;
        [SerializeField] private Text summaryText;
        [SerializeField] private Button playAgainButton;
        [SerializeField] private Button nextLevelButton;
        [SerializeField] private Button returnToLevelMenuButton;
        [SerializeField] private Color victoryTitleColor = new Color(1f, 0.72f, 0.26f, 1f);
        [SerializeField] private Color defeatTitleColor = new Color(0.93f, 0.29f, 0.25f, 1f);

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
            titleText.text = state.TitleText;
            titleText.color = state.Outcome == LevelOutcome.Defeat
                ? defeatTitleColor
                : victoryTitleColor;
            summaryText.text = state.SummaryText;
            // Hidden rather than disabled so the button row re-flows around what is left.
            nextLevelButton.gameObject.SetActive(state.NextLevelVisible);
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
