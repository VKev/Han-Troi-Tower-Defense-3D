namespace TowerDefense3D.GridPlacement
{
    /// <summary>
    /// Validates common support on the base and the entire occupied volume.
    /// </summary>
    public sealed class PlacementValidator
    {
        private readonly GridBoard board;
        private readonly GridOccupancy occupancy;

        public PlacementValidator(GridBoard board, GridOccupancy occupancy)
        {
            this.board = board;
            this.occupancy = occupancy;
        }

        public PlacementResult Evaluate(GridCell anchor, TowerFootprint footprint)
        {
            if (footprint.Width <= 0 || footprint.Depth <= 0 || footprint.Height <= 0)
            {
                return new PlacementResult(PlacementFailureFlags.OutOfBounds);
            }

            PlacementFailureFlags failures = PlacementFailureFlags.None;
            int minX = anchor.X - ((footprint.Width - 1) / 2);
            int minZ = anchor.Z - ((footprint.Depth - 1) / 2);

            for (int zOffset = 0; zOffset < footprint.Depth; zOffset++)
            {
                for (int xOffset = 0; xOffset < footprint.Width; xOffset++)
                {
                    var baseCell = new GridCell(minX + xOffset, minZ + zOffset, anchor.Y);
                    if (!board.TryGetFlags(baseCell, out BoardCellFlags baseFlags))
                    {
                        failures |= PlacementFailureFlags.OutOfBounds;
                        continue;
                    }

                    if ((baseFlags & BoardCellFlags.SupportsPlacement) == 0)
                    {
                        failures |= PlacementFailureFlags.MissingSupport;
                    }

                    if ((baseFlags & BoardCellFlags.Buildable) == 0)
                    {
                        failures |= PlacementFailureFlags.NotBuildable;
                    }
                }
            }

            for (int yOffset = 0; yOffset < footprint.Height; yOffset++)
            {
                for (int zOffset = 0; zOffset < footprint.Depth; zOffset++)
                {
                    for (int xOffset = 0; xOffset < footprint.Width; xOffset++)
                    {
                        var cell = new GridCell(
                            minX + xOffset,
                            minZ + zOffset,
                            anchor.Y + yOffset);

                        if (!board.TryGetFlags(cell, out BoardCellFlags flags))
                        {
                            failures |= PlacementFailureFlags.OutOfBounds;
                            continue;
                        }

                        if ((flags & BoardCellFlags.StaticBlocker) != 0)
                        {
                            failures |= PlacementFailureFlags.StaticBlocker;
                        }

                        if (occupancy.IsOccupied(cell))
                        {
                            failures |= PlacementFailureFlags.Occupied;
                        }
                    }
                }
            }

            return new PlacementResult(failures);
        }
    }
}
