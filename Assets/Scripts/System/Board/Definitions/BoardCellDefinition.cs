using System;
using UnityEngine;

namespace TowerDefense3D.GridPlacement
{
    public enum RoadExitDirection
    {
        None,
        East,
        South,
        West,
        North
    }

    [Flags]
    public enum BoardCellFlags
    {
        None = 0,
        SupportsPlacement = 1 << 0,
        Buildable = 1 << 1,
        StaticBlocker = 1 << 2,
        CameraFocus = 1 << 3,
        Road = 1 << 4,
        RoadSpawn = 1 << 5,
        RoadEnd = 1 << 6
    }

    /// <summary>
    /// Immutable runtime view of one authored board cell.
    /// </summary>
    [Serializable]
    public struct BoardCellDefinition
    {
        [SerializeField] private GridCell coordinate;
        [SerializeField] private BoardCellFlags flags;
        [SerializeField] private RoadExitDirection roadExitDirection;

        public BoardCellDefinition(GridCell coordinate, BoardCellFlags flags)
            : this(coordinate, flags, RoadExitDirection.None)
        {
        }

        public BoardCellDefinition(
            GridCell coordinate,
            BoardCellFlags flags,
            RoadExitDirection roadExitDirection)
        {
            this.coordinate = coordinate;
            this.flags = flags;
            this.roadExitDirection = roadExitDirection;
        }

        public GridCell Coordinate => coordinate;
        public BoardCellFlags Flags => flags;
        public bool SupportsPlacement => (flags & BoardCellFlags.SupportsPlacement) != 0;
        public bool IsBuildable => (flags & BoardCellFlags.Buildable) != 0;
        public bool IsStaticBlocker => (flags & BoardCellFlags.StaticBlocker) != 0;
        public bool IsCameraFocus => (flags & BoardCellFlags.CameraFocus) != 0;
        public bool IsRoad => (flags & BoardCellFlags.Road) != 0;
        public bool IsRoadSpawn => (flags & BoardCellFlags.RoadSpawn) != 0;
        public bool IsRoadEnd => (flags & BoardCellFlags.RoadEnd) != 0;
        public RoadExitDirection RoadExitDirection => roadExitDirection;
    }
}
