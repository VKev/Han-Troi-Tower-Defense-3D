using UnityEngine;

namespace TowerDefense3D.GridPlacement
{
    public interface IGridPlacementView
    {
        /// <summary>
        /// Projects a screen point onto the board.
        /// </summary>
        /// <param name="offsetForFinger">
        /// Whether to sample above and to the left of the pointer rather than under it. A finger
        /// dragging a tower covers the very cell it is choosing, so the drag paths ask for the
        /// lifted sample and the whole preview - footprint, link ring, and the cell the tower
        /// finally lands on - moves with it. A mouse occludes nothing and asks for the plain one.
        /// </param>
        bool TryGetWorldPoint(Vector2 screenPosition, bool offsetForFinger, out Vector3 worldPoint);

        /// <summary>
        /// Draws the candidate: the cells the tower will stand on, and the ring showing how far
        /// it will be able to link from there.
        /// </summary>
        /// <param name="linkRangeMeters">
        /// Radius of the link ring. Zero draws no ring, which is what a caller that has no
        /// network rules to consult passes.
        /// </param>
        void Show(
            TowerFootprint footprint,
            Vector3 footprintBottomCenter,
            float cellSize,
            float heightUnit,
            float linkRangeMeters,
            bool isValid);

        void Hide();
    }
}
