using System;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class BlockingErrorScreen : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Text messageLabel;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button startNewButton;
        [SerializeField] private StartNewConfirmation startNewConfirmation;

        private Action retry;
        private Action startNew;

        private void Awake()
        {
            retryButton.onClick.AddListener(HandleRetry);
            startNewButton.onClick.AddListener(HandleStartNew);
        }

        private void OnDestroy()
        {
            retryButton?.onClick.RemoveListener(HandleRetry);
            startNewButton?.onClick.RemoveListener(HandleStartNew);
        }

        public void Show(string message, Action retryAction, Action startNewAction)
        {
            retry = retryAction;
            startNew = startNewAction;
            messageLabel.text = message;
            retryButton.gameObject.SetActive(retry != null);
            startNewButton.gameObject.SetActive(startNew != null);

            SetVisible(true);
        }

        public void Hide()
        {
            retry = null;
            startNew = null;
            startNewConfirmation?.Hide();
            SetVisible(false);
        }

        private void HandleRetry()
        {
            retry();
        }

        private void HandleStartNew()
        {
            startNewConfirmation.Show(startNew);
        }

        private void SetVisible(bool visible)
        {
            root.SetActive(visible);
        }
    }
}
