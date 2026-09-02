using System;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class LevelSkipCheatView : MonoBehaviour, ILevelSkipCheatView
    {
        [SerializeField] private Button skipButton;

        private bool isInitialized;

        public event Action SkipToVictoryRequested;

        public void Initialize()
        {
            if (isInitialized)
            {
                return;
            }

            skipButton.onClick.AddListener(HandleSkipClicked);
            isInitialized = true;
        }

        public void Render(bool canSkip)
        {
            skipButton.interactable = canSkip;
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
            isInitialized = false;
        }

        private void HandleSkipClicked()
        {
            SkipToVictoryRequested?.Invoke();
        }
    }
}
