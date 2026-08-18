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
            retryButton?.onClick.AddListener(HandleRetry);
            startNewButton?.onClick.AddListener(HandleStartNew);
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
            if (messageLabel != null)
            {
                messageLabel.text = message ?? string.Empty;
            }

            if (retryButton != null)
            {
                retryButton.gameObject.SetActive(retry != null);
            }

            if (startNewButton != null)
            {
                startNewButton.gameObject.SetActive(startNew != null);
            }

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
            retry?.Invoke();
        }

        private void HandleStartNew()
        {
            if (startNew == null)
            {
                return;
            }

            if (startNewConfirmation == null)
            {
                Debug.LogError("BlockingErrorScreen requires StartNewConfirmation before destructive reset.", this);
                return;
            }

            startNewConfirmation.Show(startNew);
        }

        private void SetVisible(bool visible)
        {
            GameObject target = root != null ? root : gameObject;
            if (target.activeSelf != visible)
            {
                target.SetActive(visible);
            }
        }
    }
}
