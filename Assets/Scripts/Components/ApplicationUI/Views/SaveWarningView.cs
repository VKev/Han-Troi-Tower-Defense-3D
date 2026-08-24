using System;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class SaveWarningView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Text messageLabel;
        [SerializeField] private Button retryButton;

        private Action retrySave;

        private void Awake()
        {
            retryButton.onClick.AddListener(HandleRetry);
        }

        private void OnDestroy()
        {
            retryButton?.onClick.RemoveListener(HandleRetry);
        }

        public void Show(string message, Action retry)
        {
            retrySave = retry;
            messageLabel.text = message;
            SetVisible(true);
        }

        public void Hide()
        {
            retrySave = null;
            SetVisible(false);
        }

        private void HandleRetry()
        {
            retrySave();
        }

        private void SetVisible(bool visible)
        {
            root.SetActive(visible);
        }
    }
}
