using TowerDefense3D.GridPlacement;
using UnityEngine;

namespace TowerDefense3D.GridPlacement.Editor
{
    public enum BoardPaintPreset
    {
        Empty,
        Buildable,
        NoBuild
    }

    public static class BoardPaintPresetUtility
    {
        internal const BoardCellFlags BasicCellMask =
            BoardCellFlags.SupportsPlacement
            | BoardCellFlags.Buildable
            | BoardCellFlags.StaticBlocker;

        public static BoardCellFlags GetFlags(BoardPaintPreset preset)
        {
            switch (preset)
            {
                case BoardPaintPreset.Buildable:
                    return BoardCellFlags.SupportsPlacement | BoardCellFlags.Buildable;
                case BoardPaintPreset.NoBuild:
                    return BoardCellFlags.SupportsPlacement;
                default:
                    return BoardCellFlags.None;
            }
        }

        public static string GetLabel(BoardPaintPreset preset)
        {
            switch (preset)
            {
                case BoardPaintPreset.Buildable:
                    return "Buildable";
                case BoardPaintPreset.NoBuild:
                    return "No-Build";
                default:
                    return "Empty";
            }
        }

        public static Color GetColor(BoardPaintPreset preset)
        {
            switch (preset)
            {
                case BoardPaintPreset.Buildable:
                    return new Color(0.20f, 0.72f, 0.32f, 1f);
                case BoardPaintPreset.NoBuild:
                    return new Color(0.95f, 0.76f, 0.20f, 1f);
                default:
                    return new Color(0.24f, 0.26f, 0.29f, 1f);
            }
        }

        public static BoardPaintPreset GetClosestPreset(BoardCellFlags flags)
        {
            BoardCellFlags maskedFlags =
                flags & ~(
                    BoardCellFlags.StaticBlocker
                    | BoardCellFlags.CameraFocus
                    | RoadPaintModeUtility.RoadRoleMask);
            foreach (BoardPaintPreset preset in System.Enum.GetValues(typeof(BoardPaintPreset)))
            {
                if (GetFlags(preset) == maskedFlags)
                {
                    return preset;
                }
            }

            return BoardPaintPreset.Empty;
        }
    }
}
