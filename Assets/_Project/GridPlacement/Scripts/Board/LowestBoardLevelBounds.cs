using System;
using System.Collections.Generic;

namespace TowerDefense3D.GridPlacement
{
    public readonly struct LowestBoardLevelBounds : IEquatable<LowestBoardLevelBounds>
    {
        public LowestBoardLevelBounds(
            int level,
            int minX,
            int minZ,
            int maxXExclusive,
            int maxZExclusive)
        {
            Level = level;
            MinX = minX;
            MinZ = minZ;
            MaxXExclusive = maxXExclusive;
            MaxZExclusive = maxZExclusive;
        }

        public int Level { get; }
        public int MinX { get; }
        public int MinZ { get; }
        public int MaxXExclusive { get; }
        public int MaxZExclusive { get; }

        public bool Equals(LowestBoardLevelBounds other) =>
            Level == other.Level
            && MinX == other.MinX
            && MinZ == other.MinZ
            && MaxXExclusive == other.MaxXExclusive
            && MaxZExclusive == other.MaxZExclusive;

        public override bool Equals(object obj) =>
            obj is LowestBoardLevelBounds other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Level;
                hash = (hash * 397) ^ MinX;
                hash = (hash * 397) ^ MinZ;
                hash = (hash * 397) ^ MaxXExclusive;
                return (hash * 397) ^ MaxZExclusive;
            }
        }

        public static bool operator ==(
            LowestBoardLevelBounds left,
            LowestBoardLevelBounds right) => left.Equals(right);

        public static bool operator !=(
            LowestBoardLevelBounds left,
            LowestBoardLevelBounds right) => !left.Equals(right);
    }

    public static class LowestBoardLevelBoundsCalculator
    {
        private const BoardCellFlags VisibleFootprintFlags =
            BoardCellFlags.SupportsPlacement | BoardCellFlags.StaticBlocker;

        public static bool TryCalculate(
            BoardDefinition board,
            out LowestBoardLevelBounds bounds)
        {
            bounds = default;
            if (board == null || !HasValidDimensions(board.Dimensions))
            {
                return false;
            }

            IReadOnlyList<BoardCellDefinition> cells = board.Cells;
            if (cells == null)
            {
                return false;
            }

            int lowestLevel = int.MaxValue;
            for (int index = 0; index < cells.Count; index++)
            {
                BoardCellDefinition cell = cells[index];
                if (cell.SupportsPlacement
                    && IsWithinBounds(cell.Coordinate, board.Dimensions)
                    && cell.Coordinate.Y < lowestLevel)
                {
                    lowestLevel = cell.Coordinate.Y;
                }
            }

            if (lowestLevel == int.MaxValue)
            {
                return false;
            }

            int minX = int.MaxValue;
            int minZ = int.MaxValue;
            int maxX = int.MinValue;
            int maxZ = int.MinValue;

            for (int index = 0; index < cells.Count; index++)
            {
                BoardCellDefinition cell = cells[index];
                GridCell coordinate = cell.Coordinate;
                if (coordinate.Y != lowestLevel
                    || !IsWithinBounds(coordinate, board.Dimensions)
                    || (cell.Flags & VisibleFootprintFlags) == 0)
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

            bounds = new LowestBoardLevelBounds(
                lowestLevel,
                minX,
                minZ,
                maxX + 1,
                maxZ + 1);
            return true;
        }

        private static bool HasValidDimensions(GridDimensions dimensions) =>
            dimensions.Width > 0 && dimensions.Depth > 0 && dimensions.Height > 0;

        private static bool IsWithinBounds(
            GridCell cell,
            GridDimensions dimensions) =>
            cell.X >= 0 && cell.X < dimensions.Width
            && cell.Z >= 0 && cell.Z < dimensions.Depth
            && cell.Y >= 0 && cell.Y < dimensions.Height;
    }
}
