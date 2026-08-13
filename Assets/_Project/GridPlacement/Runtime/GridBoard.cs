using System;
using UnityEngine;

namespace TowerDefense3D.GridPlacement
{
    /// <summary>
    /// Immutable runtime copy of authored board support, buildability and blockers.
    /// </summary>
    public sealed class GridBoard
    {
        private readonly GridDimensions dimensions;
        private readonly BoardCellFlags[] cellFlags;

        public GridBoard(BoardDefinition definition, Vector3 worldOrigin)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            dimensions = definition.Dimensions;
            ValidateDimensions(dimensions);
            cellFlags = new BoardCellFlags[checked(dimensions.Width * dimensions.Depth * dimensions.Height)];
            Mapper = new GridCoordinateMapper(dimensions, definition.CellSize, definition.HeightUnit, worldOrigin);

            for (int i = 0; i < definition.Cells.Count; i++)
            {
                BoardCellDefinition authoredCell = definition.Cells[i];
                if (TryGetIndex(authoredCell.Coordinate, out int index))
                {
                    cellFlags[index] |= authoredCell.Flags;
                }
            }
        }

        public GridDimensions Dimensions => dimensions;
        public GridCoordinateMapper Mapper { get; }

        public bool IsWithinBounds(GridCell cell) => TryGetIndex(cell, out _);

        public bool TryGetFlags(GridCell cell, out BoardCellFlags flags)
        {
            if (!TryGetIndex(cell, out int index))
            {
                flags = BoardCellFlags.None;
                return false;
            }

            flags = cellFlags[index];
            return true;
        }

        public bool SupportsPlacement(GridCell cell) =>
            TryGetFlags(cell, out BoardCellFlags flags)
            && (flags & BoardCellFlags.SupportsPlacement) != 0;

        public bool IsBuildable(GridCell cell) =>
            TryGetFlags(cell, out BoardCellFlags flags)
            && (flags & BoardCellFlags.Buildable) != 0;

        public bool IsStaticBlocker(GridCell cell) =>
            TryGetFlags(cell, out BoardCellFlags flags)
            && (flags & BoardCellFlags.StaticBlocker) != 0;

        public bool TryGetIndex(GridCell cell, out int index)
        {
            if (cell.X < 0 || cell.X >= dimensions.Width
                || cell.Z < 0 || cell.Z >= dimensions.Depth
                || cell.Y < 0 || cell.Y >= dimensions.Height)
            {
                index = -1;
                return false;
            }

            index = cell.X
                + (cell.Z * dimensions.Width)
                + (cell.Y * dimensions.Width * dimensions.Depth);
            return true;
        }

        private static void ValidateDimensions(GridDimensions value)
        {
            if (value.Width <= 0 || value.Depth <= 0 || value.Height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Board dimensions must be positive.");
            }
        }
    }
}
