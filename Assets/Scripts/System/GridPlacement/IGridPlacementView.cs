using UnityEngine;

namespace TowerDefense3D.GridPlacement
{
    public interface IGridPlacementView
    {
        bool TryGetWorldPoint(Vector2 screenPosition, out Vector3 worldPoint);

        void Show(
            TowerFootprint footprint,
            Vector3 footprintBottomCenter,
            float cellSize,
            float heightUnit,
            bool isValid);

        void Hide();
    }
}
