using UnityEngine;

namespace TowerDefense3D.GridPlacement
{
    /// <summary>
    /// Exposes authored RectTransform and current screen values to SafeAreaSystem.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaView : MonoBehaviour, ISafeAreaView
    {
        [SerializeField] private RectTransform target;

        public Rect SafeArea => Screen.safeArea;
        public Vector2Int ScreenSize => new Vector2Int(Screen.width, Screen.height);

        private void OnEnable()
        {
            if (target == null)
            {
                target = (RectTransform)transform;
            }
        }

        public void ApplyAnchors(Vector2 anchorMin, Vector2 anchorMax)
        {
            target.anchorMin = anchorMin;
            target.anchorMax = anchorMax;
            target.offsetMin = Vector2.zero;
            target.offsetMax = Vector2.zero;
        }
    }
}
