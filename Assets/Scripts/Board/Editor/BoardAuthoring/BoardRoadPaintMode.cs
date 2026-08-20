using TowerDefense3D.GridPlacement;
using UnityEngine;

namespace TowerDefense3D.GridPlacement.Editor
{
    public enum RoadPaintMode
    {
        None,
        Road,
        Spawn,
        End
    }

    public static class RoadPaintModeUtility
    {
        internal const BoardCellFlags RoadRoleMask =
            BoardCellFlags.Road | BoardCellFlags.RoadSpawn | BoardCellFlags.RoadEnd;

        public static BoardCellFlags GetFlags(RoadPaintMode mode) => mode switch
        {
            RoadPaintMode.Road => BoardCellFlags.Road,
            RoadPaintMode.Spawn => BoardCellFlags.RoadSpawn,
            RoadPaintMode.End => BoardCellFlags.RoadEnd,
            _ => BoardCellFlags.None
        };

        public static string GetLabel(RoadPaintMode mode) => mode switch
        {
            RoadPaintMode.Road => "Road",
            RoadPaintMode.Spawn => "Spawn",
            RoadPaintMode.End => "End",
            _ => "Erase"
        };

        public static Color GetColor(RoadPaintMode mode) => mode switch
        {
            RoadPaintMode.Road => new Color(0.55f, 0.40f, 0.20f, 1f),   // earthy brown
            RoadPaintMode.Spawn => new Color(0.20f, 0.55f, 0.95f, 1f),  // bright blue
            RoadPaintMode.End => new Color(0.90f, 0.20f, 0.55f, 1f),    // magenta/pink
            _ => Color.clear
        };

        public static RoadPaintMode GetRoadRole(BoardCellFlags flags)
        {
            BoardCellFlags masked = flags & RoadRoleMask;
            if ((masked & BoardCellFlags.RoadSpawn) != 0) return RoadPaintMode.Spawn;
            if ((masked & BoardCellFlags.RoadEnd) != 0) return RoadPaintMode.End;
            if ((masked & BoardCellFlags.Road) != 0) return RoadPaintMode.Road;
            return RoadPaintMode.None;
        }
    }
}
