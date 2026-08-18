using System;
using UnityEngine;

namespace TowerDefense3D.GridPlacement
{
    [Flags]
    public enum BoardCellFlags
    {
        None = 0,
        SupportsPlacement = 1 << 0,
        Buildable = 1 << 1,
        StaticBlocker = 1 << 2,
        CameraFocus = 1 << 3
    }

    /// <summary>
    /// Immutable runtime view of one authored board cell.
    /// </summary>
    [Serializable]
    public struct BoardCellDefinition
    {
        [SerializeField] private GridCell coordinate;
        [SerializeField] private BoardCellFlags flags;

        public BoardCellDefinition(GridCell coordinate, BoardCellFlags flags)
        {
            this.coordinate = coordinate;
            this.flags = flags;
        }

        public GridCell Coordinate => coordinate;
        public BoardCellFlags Flags => flags;
        public bool SupportsPlacement => (flags & BoardCellFlags.SupportsPlacement) != 0;
        public bool IsBuildable => (flags & BoardCellFlags.Buildable) != 0;
        public bool IsStaticBlocker => (flags & BoardCellFlags.StaticBlocker) != 0;
        public bool IsCameraFocus => (flags & BoardCellFlags.CameraFocus) != 0;
    }
}
