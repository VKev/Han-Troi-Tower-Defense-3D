using System;
using UnityEngine;

namespace TowerDefense3D.GridPlacement
{
    /// <summary>
    /// Identifies one grid cell. X and Z are horizontal; Y is vertical.
    /// </summary>
    [Serializable]
    public struct GridCell : IEquatable<GridCell>
    {
        [SerializeField] private int x;
        [SerializeField] private int z;
        [SerializeField] private int y;

        public GridCell(int x, int z, int y)
        {
            this.x = x;
            this.z = z;
            this.y = y;
        }

        public int X => x;
        public int Z => z;
        public int Y => y;

        public bool Equals(GridCell other) =>
            x == other.x && z == other.z && y == other.y;

        public override bool Equals(object obj) =>
            obj is GridCell other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = x;
                hash = (hash * 397) ^ z;
                return (hash * 397) ^ y;
            }
        }

        public override string ToString() => $"({x}, {z}, {y})";

        public static bool operator ==(GridCell left, GridCell right) => left.Equals(right);
        public static bool operator !=(GridCell left, GridCell right) => !left.Equals(right);
    }
}
