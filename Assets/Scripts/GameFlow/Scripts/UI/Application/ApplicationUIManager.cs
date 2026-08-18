using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Persistent Bootstrap-owned presentation facade. Domain and transition decisions stay in GameFlowCoordinator.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ApplicationUIManager : MonoBehaviour, IApplicationUI
    {
        [SerializeField] private LevelMenuScreen levelMenuScreen;
        [SerializeField] private LoadingScreen loadingScreen;
        [SerializeField] private BlockingErrorScreen blockingErrorScreen;
        [SerializeField] private SaveWarningView saveWarningView;
        [SerializeField] private GameObject inputBlocker;

        private void Awake()
        {
            levelMenuScreen?.Hide();
            loadingScreen?.Hide();
            blockingErrorScreen?.Hide();
            saveWarningView?.Hide();
            SetInputBlocked(false);
        }

        public void ShowLevelMenu(IReadOnlyList<LevelMenuItemState> levels, Action<int> onLevelSelected)
        {
            levelMenuScreen?.Show(levels, onLevelSelected);
        }

        public void HideLevelMenu()
        {
            levelMenuScreen?.Hide();
        }

        public void ShowLoading(string message)
        {
            loadingScreen?.Show(message);
        }

        public void HideLoading()
        {
            loadingScreen?.Hide();
        }

        public void ShowBlockingError(string message, Action retry, Action startNew)
        {
            blockingErrorScreen?.Show(message, retry, startNew);
        }

        public void HideBlockingError()
        {
            blockingErrorScreen?.Hide();
        }

        public void ShowSaveWarning(string message, Action retrySave)
        {
            saveWarningView?.Show(message, retrySave);
        }

        public void HideSaveWarning()
        {
            saveWarningView?.Hide();
        }

        public void SetInputBlocked(bool isBlocked)
        {
            if (inputBlocker != null)
            {
                inputBlocker.SetActive(isBlocked);
            }
        }
    }
}
