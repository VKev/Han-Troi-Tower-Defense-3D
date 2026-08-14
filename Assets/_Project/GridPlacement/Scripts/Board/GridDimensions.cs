using System;
using UnityEngine;

namespace TowerDefense3D.GridPlacement
{
    /// <summary>
    /// Board dimensions in cells: Width is X, Depth is Z, and Height is Y.
    /// </summary>
    [Serializable]
    public struct GridDimensions : IEquatable<GridDimensions>
    {
        [SerializeField, Min(1)] private int width;
        [SerializeField, Min(1)] private int depth;
        [SerializeField, Min(1)] private int height;

        public GridDimensions(int width, int depth, int height)
        {
            this.width = width;
            this.depth = depth;
            this.height = height;
        }

        public int Width => width;
        public int Depth => depth;
        public int Height => height;

        public bool Equals(GridDimensions other) =>
            width == other.width && depth == other.depth && height == other.height;

        public override bool Equals(object obj) =>
            obj is GridDimensions other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = width;
                hash = (hash * 397) ^ depth;
                return (hash * 397) ^ height;
            }
        }

        public override string ToString() => $"{width}x{depth}x{height}";

        public static bool operator ==(GridDimensions left, GridDimensions right) => left.Equals(right);
        public static bool operator !=(GridDimensions left, GridDimensions right) => !left.Equals(right);
    }
}
