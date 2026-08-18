using UnityEngine;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Owns visibility for the placement instructions, selection panel, and cancel control.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlacementHudView : MonoBehaviour
    {
        [SerializeField] private GameObject root;

        public void Show()
        {
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
