using TowerDefense3D.GridPlacement;
using UnityEngine;

namespace TowerDefense3D.GridPlacement.Editor
{
    public enum BoardPaintPreset
    {
        Empty,
        Buildable,
        NoBuild,
        BlockedSurface,
        VolumeBlocker
    }

    public static class BoardPaintPresetUtility
    {
        public static BoardCellFlags GetFlags(BoardPaintPreset preset)
        {
            switch (preset)
            {
                case BoardPaintPreset.Buildable:
                    return BoardCellFlags.SupportsPlacement | BoardCellFlags.Buildable;
                case BoardPaintPreset.NoBuild:
                    return BoardCellFlags.SupportsPlacement;
                case BoardPaintPreset.BlockedSurface:
                    return BoardCellFlags.SupportsPlacement
                        | BoardCellFlags.Buildable
                        | BoardCellFlags.StaticBlocker;
                case BoardPaintPreset.VolumeBlocker:
                    return BoardCellFlags.StaticBlocker;
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
                case BoardPaintPreset.BlockedSurface:
                    return "Blocked Surface";
                case BoardPaintPreset.VolumeBlocker:
                    return "Volume Blocker";
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
                case BoardPaintPreset.BlockedSurface:
                    return new Color(0.86f, 0.24f, 0.20f, 1f);
                case BoardPaintPreset.VolumeBlocker:
                    return new Color(0.58f, 0.30f, 0.78f, 1f);
                default:
                    return new Color(0.24f, 0.26f, 0.29f, 1f);
            }
        }

        public static BoardPaintPreset GetClosestPreset(BoardCellFlags flags)
        {
            BoardCellFlags maskedFlags = flags & ~BoardCellFlags.CameraFocus;
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
