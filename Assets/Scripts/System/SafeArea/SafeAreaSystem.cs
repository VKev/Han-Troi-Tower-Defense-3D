using UnityEngine;

namespace TowerDefense3D.GridPlacement
{
    /// <summary>
    /// Projects the current device safe area into normalized UI anchors when screen inputs change.
    /// </summary>
    public sealed class SafeAreaSystem
    {
        private readonly ISafeAreaView view;

        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;
        private bool hasAppliedSafeArea;

        public SafeAreaSystem(ISafeAreaView view)
        {
            this.view = view;
        }

        public void Start()
        {
            Refresh(true);
        }

        public void Tick()
        {
            Refresh(false);
        }

        private void Refresh(bool force)
        {
            Vector2Int screenSize = view.ScreenSize;
            if (screenSize.x <= 0 || screenSize.y <= 0)
            {
                return;
            }

            Rect safeArea = view.SafeArea;
            if (!force && hasAppliedSafeArea && safeArea == lastSafeArea && screenSize == lastScreenSize)
            {
                return;
            }

            lastSafeArea = safeArea;
            lastScreenSize = screenSize;
            hasAppliedSafeArea = true;

            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;
            anchorMin.x /= screenSize.x;
            anchorMin.y /= screenSize.y;
            anchorMax.x /= screenSize.x;
            anchorMax.y /= screenSize.y;
            view.ApplyAnchors(anchorMin, anchorMax);
        }
    }
}
