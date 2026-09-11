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
        [Tooltip("Sorting override that keeps this modal above everything else, the tutorial overlay included. Without it the modal draws inside the gameplay canvas and the tutorial's dimmer and running text cover it.")]
        [SerializeField] private Canvas modalCanvas;

        /// <summary>
        /// Above the tutorial overlay, which sorts at 100.
        /// </summary>
        /// <remarks>
        /// A modal is a question put to the player, so nothing may draw over it - and a tutorial
        /// beat can still be mid-sentence when one opens. The order is only claimed while the
        /// modal is up; a hidden modal holding the top of the stack would keep raycasts that
        /// belong to the HUD underneath it.
        /// </remarks>
        private const int ModalSortingOrder = 200;

        private void ApplyModalSorting(bool visible)
        {
            if (modalCanvas == null)
            {
                return;
            }

            modalCanvas.overrideSorting = visible;
            if (visible)
            {
                modalCanvas.sortingOrder = ModalSortingOrder;
            }
        }

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
                ApplyModalSorting(true);
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
                ApplyModalSorting(false);
                return;
            }

            RectTransform hideRect = root.transform as RectTransform;
            visibilityTween = DOTween.Sequence()
                .Join(hideRect.DOScale(0.92f, 0.16f).SetEase(Ease.InSine))
                .OnComplete(() =>
                {
                    root.SetActive(false);
                    ApplyModalSorting(false);
                })
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
