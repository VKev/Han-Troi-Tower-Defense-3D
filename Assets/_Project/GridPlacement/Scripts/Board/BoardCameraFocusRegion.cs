using System;
using System.Collections.Generic;

namespace TowerDefense3D.GridPlacement
{
    /// <summary>
    /// Narrows the lowest playable level's full footprint to the union of
    /// cells at that level flagged <see cref="BoardCellFlags.CameraFocus"/>.
    /// </summary>
    public static class BoardCameraFocusRegionCalculator
    {
        public static bool TryCalculate(
            BoardDefinition board,
            LowestBoardLevelBounds lowestLevelBounds,
            out LowestBoardLevelBounds focusBounds)
        {
            focusBounds = default;
            if (board == null)
            {
                return false;
            }

            IReadOnlyList<BoardCellDefinition> cells = board.Cells;
            if (cells == null)
            {
                return false;
            }

            GridDimensions dimensions = board.Dimensions;

            int minX = int.MaxValue;
            int minZ = int.MaxValue;
            int maxX = int.MinValue;
            int maxZ = int.MinValue;

            for (int index = 0; index < cells.Count; index++)
            {
                BoardCellDefinition cell = cells[index];
                GridCell coordinate = cell.Coordinate;
                if (coordinate.Y != lowestLevelBounds.Level
                    || !cell.IsCameraFocus
                    || !IsWithinBounds(coordinate, dimensions))
                {
                    continue;
                }

                minX = Math.Min(minX, coordinate.X);
                minZ = Math.Min(minZ, coordinate.Z);
                maxX = Math.Max(maxX, coordinate.X);
                maxZ = Math.Max(maxZ, coordinate.Z);
            }

            if (minX == int.MaxValue)
            {
                return false;
            }

            focusBounds = new LowestBoardLevelBounds(
                lowestLevelBounds.Level,
                minX,
                minZ,
                maxX + 1,
                maxZ + 1);
            return true;
        }

        private static bool IsWithinBounds(
            GridCell cell,
            GridDimensions dimensions) =>
            cell.X >= 0 && cell.X < dimensions.Width
            && cell.Z >= 0 && cell.Z < dimensions.Depth
            && cell.Y >= 0 && cell.Y < dimensions.Height;
    }
}
