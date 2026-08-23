using UnityEngine;

namespace TowerDefense3D.GridPlacement
{
    public interface ISafeAreaView
    {
        Rect SafeArea { get; }
        Vector2Int ScreenSize { get; }

        void ApplyAnchors(Vector2 anchorMin, Vector2 anchorMax);
    }
}
