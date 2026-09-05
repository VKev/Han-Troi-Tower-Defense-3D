using System;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class PauseMenuHudView : MonoBehaviour, IPauseMenuHudView
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button returnToLevelMenuButton;

        private bool isInitialized;

        public event Action ResumeRequested;
        public event Action RestartRequested;
        public event Action ReturnToLevelMenuRequested;

        public void Initialize()
        {
            if (isInitialized)
            {
                return;
            }

            resumeButton.onClick.AddListener(HandleResumeClicked);
            restartButton.onClick.AddListener(HandleRestartClicked);
            returnToLevelMenuButton.onClick.AddListener(HandleReturnToLevelMenuClicked);
            isInitialized = true;
        }

        public void Render(PauseMenuHudState state)
        {
            root.SetActive(state.IsVisible);
        }

        public void Shutdown()
        {
            if (!isInitialized)
            {
                return;
            }

            resumeButton.onClick.RemoveListener(HandleResumeClicked);
            restartButton.onClick.RemoveListener(HandleRestartClicked);
            returnToLevelMenuButton.onClick.RemoveListener(HandleReturnToLevelMenuClicked);
            isInitialized = false;
        }

        private void HandleResumeClicked()
        {
            ResumeRequested?.Invoke();
        }

        private void HandleRestartClicked()
        {
            RestartRequested?.Invoke();
        }

        private void HandleReturnToLevelMenuClicked()
        {
            ReturnToLevelMenuRequested?.Invoke();
        }
    }
}
