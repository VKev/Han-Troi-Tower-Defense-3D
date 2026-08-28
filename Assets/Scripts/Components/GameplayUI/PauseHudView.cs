using System;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class PauseHudView : MonoBehaviour, IPauseHudView
    {
        [SerializeField] private Button pauseButton;
        [SerializeField] private Text pauseText;

        private bool isInitialized;

        public event Action PauseToggleRequested;

        public void Initialize()
        {
            if (isInitialized)
            {
                return;
            }

            pauseButton.onClick.AddListener(HandlePauseClicked);
            isInitialized = true;
        }

        public void Render(bool isPaused)
        {
            pauseText.text = isPaused ? "▶" : "Ⅱ";
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

            pauseButton.onClick.RemoveListener(HandlePauseClicked);
            isInitialized = false;
        }

        private void HandlePauseClicked()
        {
            PauseToggleRequested?.Invoke();
        }
    }
}
