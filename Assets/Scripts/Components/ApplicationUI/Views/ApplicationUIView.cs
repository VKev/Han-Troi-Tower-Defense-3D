using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Persistent Bootstrap-owned composite view for authored application UI.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ApplicationUIView : MonoBehaviour, IApplicationUIView
    {
        [SerializeField] private LevelMenuView levelMenuView;
        [SerializeField] private LoadingView loadingView;
        [SerializeField] private BlockingErrorView blockingErrorView;
        [SerializeField] private SaveWarningView saveWarningView;
        [SerializeField] private GameObject inputBlocker;

        public void Reset()
        {
            if (levelMenuView != null)
            {
                levelMenuView.Hide();
            }

            if (loadingView != null)
            {
                loadingView.Hide();
            }

            if (blockingErrorView != null)
            {
                blockingErrorView.Hide();
            }

            if (saveWarningView != null)
            {
                saveWarningView.Hide();
            }

            if (inputBlocker != null)
            {
                inputBlocker.SetActive(false);
            }
        }

        public void ShowLevelMenu(IReadOnlyList<LevelMenuItemState> levels, Action<int> onLevelSelected)
        {
            levelMenuView.Show(levels, onLevelSelected);
        }

        public void HideLevelMenu()
        {
            levelMenuView.Hide();
        }

        public void ShowLoading(string message)
        {
            loadingView.Show(message);
        }

        public void HideLoading()
        {
            loadingView.Hide();
        }

        public void ShowBlockingError(string message, Action retry, Action startNew)
        {
            blockingErrorView.Show(message, retry, startNew);
        }

        public void HideBlockingError()
        {
            blockingErrorView.Hide();
        }

        public void ShowSaveWarning(string message, Action retrySave)
        {
            saveWarningView.Show(message, retrySave);
        }

        public void HideSaveWarning()
        {
            saveWarningView.Hide();
        }

        public void SetInputBlocked(bool isBlocked)
        {
            inputBlocker.SetActive(isBlocked);
        }
    }
}
