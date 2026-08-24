using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class LoadingView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Text messageLabel;

        public void Show(string message)
        {
            messageLabel.text = message;
            SetVisible(true);
        }

        public void Hide()
        {
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            root.SetActive(visible);
        }
    }
}
