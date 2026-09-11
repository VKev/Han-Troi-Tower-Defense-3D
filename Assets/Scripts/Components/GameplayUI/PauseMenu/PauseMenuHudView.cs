using System;
using DG.Tweening;
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
        private Tween visibilityTween;

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
            visibilityTween?.Kill();
            visibilityTween = null;
            if (state.IsVisible)
            {
                root.SetActive(true);
                RectTransform rect = root.transform as RectTransform;
                rect.localScale = Vector3.one * 0.92f;
                visibilityTween = DOTween.Sequence()
                    .Join(rect.DOScale(1f, 0.24f).SetEase(Ease.OutBack))
                    .SetUpdate(true)
                    .SetTarget(this);
                return;
            }

            if (!root.activeSelf)
            {
                return;
            }

            RectTransform hideRect = root.transform as RectTransform;
            visibilityTween = DOTween.Sequence()
                .Join(hideRect.DOScale(0.92f, 0.16f).SetEase(Ease.InSine))
                .OnComplete(() => root.SetActive(false))
                .SetUpdate(true)
                .SetTarget(this);
        }

        public void Shutdown()
        {
            visibilityTween?.Kill();
            visibilityTween = null;
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
