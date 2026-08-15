using System;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class StartNewConfirmation : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        private Action confirm;

        private void Awake()
        {
            confirmButton?.onClick.AddListener(HandleConfirm);
            cancelButton?.onClick.AddListener(Hide);
        }

        private void OnDestroy()
        {
            confirmButton?.onClick.RemoveListener(HandleConfirm);
            cancelButton?.onClick.RemoveListener(Hide);
        }

        public void Show(Action confirmAction)
        {
            confirm = confirmAction;
            SetVisible(true);
        }

        public void Hide()
        {
            confirm = null;
            SetVisible(false);
        }

        private void HandleConfirm()
        {
            Action action = confirm;
            Hide();
            action?.Invoke();
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
