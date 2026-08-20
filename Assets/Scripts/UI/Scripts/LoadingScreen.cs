using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class LoadingScreen : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Text messageLabel;

        public void Show(string message)
        {
            if (messageLabel != null)
            {
                messageLabel.text = message ?? string.Empty;
            }

            SetVisible(true);
        }

        public void Hide()
        {
            SetVisible(false);
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
