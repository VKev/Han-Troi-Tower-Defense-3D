using System;

namespace TowerDefense3D.GridPlacement
{
    /// <summary>
    /// Enumerates footprint cells without allocating. Even dimensions extend toward +X/+Z.
    /// </summary>
    public static class FootprintEnumerator
    {
        public static int RequiredBaseCellCount(TowerFootprint footprint)
        {
            return footprint.Width > 0 && footprint.Depth > 0
                ? checked(footprint.Width * footprint.Depth)
                : 0;
        }

        public static int RequiredVolumeCellCount(TowerFootprint footprint)
        {
            return footprint.Width > 0 && footprint.Depth > 0 && footprint.Height > 0
                ? checked(footprint.Width * footprint.Depth * footprint.Height)
                : 0;
        }

        public static bool TryWriteBaseCells(
            GridCell anchor,
            TowerFootprint footprint,
            GridCell[] destination,
            out int written)
        {
            int required = RequiredBaseCellCount(footprint);
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            if (required == 0 || destination.Length < required)
            {
                written = 0;
                return false;
            }

            int minX = anchor.X - ((footprint.Width - 1) / 2);
            int minZ = anchor.Z - ((footprint.Depth - 1) / 2);
            written = 0;

            for (int zOffset = 0; zOffset < footprint.Depth; zOffset++)
            {
                for (int xOffset = 0; xOffset < footprint.Width; xOffset++)
                {
                    destination[written++] = new GridCell(
                        minX + xOffset,
                        minZ + zOffset,
                        anchor.Y);
                }
            }

            return true;
        }

        public static bool TryWriteVolumeCells(
            GridCell anchor,
            TowerFootprint footprint,
            GridCell[] destination,
            out int written)
        {
            int required = RequiredVolumeCellCount(footprint);
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            if (required == 0 || destination.Length < required)
            {
                written = 0;
                return false;
            }

            int minX = anchor.X - ((footprint.Width - 1) / 2);
            int minZ = anchor.Z - ((footprint.Depth - 1) / 2);
            written = 0;

            for (int yOffset = 0; yOffset < footprint.Height; yOffset++)
            {
                for (int zOffset = 0; zOffset < footprint.Depth; zOffset++)
                {
                    for (int xOffset = 0; xOffset < footprint.Width; xOffset++)
                    {
                        destination[written++] = new GridCell(
                            minX + xOffset,
                            minZ + zOffset,
                            anchor.Y + yOffset);
                    }
                }
            }

            return true;
        }
    }
}
