using System;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class LevelSkipCheatView : MonoBehaviour, ILevelSkipCheatView
    {
        [Tooltip("Skips every remaining wave and declares the level won.")]
        [SerializeField] private Button skipButton;

        [Tooltip("Skips only the current wave. Invisible like the other one - it is a development shortcut, not a control the player is offered.")]
        [SerializeField] private Button skipWaveButton;

        private bool isInitialized;

        public event Action SkipToVictoryRequested;

        public event Action SkipWaveRequested;

        public void Initialize()
        {
            if (isInitialized)
            {
                return;
            }

            skipButton.onClick.AddListener(HandleSkipClicked);
            if (skipWaveButton != null)
            {
                skipWaveButton.onClick.AddListener(HandleSkipWaveClicked);
            }

            isInitialized = true;
        }

        public void Render(bool canSkip)
        {
            skipButton.interactable = canSkip;
            if (skipWaveButton != null)
            {
                skipWaveButton.interactable = canSkip;
            }
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Shutdown()
        {
            if (!isInitialized)
            {
                return;
            }

            skipButton.onClick.RemoveListener(HandleSkipClicked);
            if (skipWaveButton != null)
            {
                skipWaveButton.onClick.RemoveListener(HandleSkipWaveClicked);
            }

            isInitialized = false;
        }

        private void HandleSkipClicked()
        {
            SkipToVictoryRequested?.Invoke();
        }

        private void HandleSkipWaveClicked()
        {
            SkipWaveRequested?.Invoke();
        }
    }
}
