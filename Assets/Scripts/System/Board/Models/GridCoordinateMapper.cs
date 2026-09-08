using UnityEngine;

namespace TowerDefense3D.GridPlacement
{
    /// <summary>
    /// Converts between world space and the board's discrete X/Z plane and Y levels.
    /// </summary>
    public sealed class GridCoordinateMapper
    {
        private readonly GridDimensions dimensions;
        private readonly float cellSize;
        private readonly float heightUnit;
        private readonly Vector3 worldOrigin;

        public GridCoordinateMapper(
            GridDimensions dimensions,
            float cellSize,
            float heightUnit,
            Vector3 worldOrigin)
        {
            this.dimensions = dimensions;
            this.cellSize = Mathf.Max(0.0001f, cellSize);
            this.heightUnit = Mathf.Max(0.0001f, heightUnit);
            this.worldOrigin = worldOrigin;
        }

        public bool TryWorldToCell(Vector3 worldPosition, out GridCell cell)
        {
            Vector3 local = worldPosition - worldOrigin;
            int x = Mathf.FloorToInt(local.x / cellSize);
            int z = Mathf.FloorToInt(local.z / cellSize);
            int y = Mathf.RoundToInt(local.y / heightUnit);

            cell = new GridCell(x, z, y);
            return IsWithinBounds(cell);
        }

        public Vector3 CellToWorldCenter(GridCell cell)
        {
            return worldOrigin + new Vector3(
                (cell.X + 0.5f) * cellSize,
                cell.Y * heightUnit,
                (cell.Z + 0.5f) * cellSize);
        }

        public Vector3 FootprintBottomCenter(GridCell anchor, TowerFootprint footprint)
        {
            Vector3 center = CellToWorldCenter(anchor);
            if ((footprint.Width & 1) == 0)
            {
                center.x += cellSize * 0.5f;
            }

            if ((footprint.Depth & 1) == 0)
            {
                center.z += cellSize * 0.5f;
            }

            return center;
        }

        private bool IsWithinBounds(GridCell cell)
        {
            return cell.X >= 0 && cell.X < dimensions.Width
                && cell.Z >= 0 && cell.Z < dimensions.Depth
                && cell.Y >= 0 && cell.Y < dimensions.Height;
        }
    }
}
