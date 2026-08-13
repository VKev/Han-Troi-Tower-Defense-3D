using System;

namespace TowerDefense3D.GridPlacement
{
    /// <summary>
    /// Managed flat-array ownership store. Zero is free, negative values are reservations.
    /// </summary>
    public sealed class GridOccupancy
    {
        private readonly GridDimensions dimensions;
        private readonly int[] owners;
        private int nextReservationToken = 1;

        public GridOccupancy(GridDimensions dimensions)
        {
            if (dimensions.Width <= 0 || dimensions.Depth <= 0 || dimensions.Height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(dimensions));
            }

            this.dimensions = dimensions;
            owners = new int[checked(dimensions.Width * dimensions.Depth * dimensions.Height)];
        }

        public GridDimensions Dimensions => dimensions;

        public bool IsOccupied(GridCell cell)
        {
            return TryGetIndex(cell, out int index) && owners[index] != 0;
        }

        public bool TryGetOwner(GridCell cell, out int ownerId)
        {
            if (!TryGetIndex(cell, out int index))
            {
                ownerId = 0;
                return false;
            }

            ownerId = owners[index] > 0 ? owners[index] : 0;
            return true;
        }

        public bool TryReserve(
            GridCell anchor,
            TowerFootprint footprint,
            out PlacementReservation reservation)
        {
            int required = FootprintEnumerator.RequiredVolumeCellCount(footprint);
            if (required == 0)
            {
                reservation = null;
                return false;
            }

            var cells = new GridCell[required];
            if (!FootprintEnumerator.TryWriteVolumeCells(anchor, footprint, cells, out int written))
            {
                reservation = null;
                return false;
            }

            for (int i = 0; i < written; i++)
            {
                if (!TryGetIndex(cells[i], out int index) || owners[index] != 0)
                {
                    reservation = null;
                    return false;
                }
            }

            int token = NextReservationToken();
            int reservedValue = -token;
            for (int i = 0; i < written; i++)
            {
                TryGetIndex(cells[i], out int index);
                owners[index] = reservedValue;
            }

            reservation = new PlacementReservation(this, cells, reservedValue);
            return true;
        }

        internal bool Commit(GridCell[] cells, int reservedValue, int ownerId)
        {
            if (ownerId <= 0 || !AllCellsMatch(cells, reservedValue))
            {
                return false;
            }

            for (int i = 0; i < cells.Length; i++)
            {
                TryGetIndex(cells[i], out int index);
                owners[index] = ownerId;
            }

            return true;
        }

        internal void Rollback(GridCell[] cells, int reservedValue)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                if (TryGetIndex(cells[i], out int index) && owners[index] == reservedValue)
                {
                    owners[index] = 0;
                }
            }
        }

        public void ReleaseOwner(int ownerId)
        {
            if (ownerId <= 0)
            {
                return;
            }

            for (int i = 0; i < owners.Length; i++)
            {
                if (owners[i] == ownerId)
                {
                    owners[i] = 0;
                }
            }
        }

        private bool AllCellsMatch(GridCell[] cells, int value)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                if (!TryGetIndex(cells[i], out int index) || owners[index] != value)
                {
                    return false;
                }
            }

            return true;
        }

        private int NextReservationToken()
        {
            if (nextReservationToken == int.MaxValue)
            {
                nextReservationToken = 1;
            }

            return nextReservationToken++;
        }

        private bool TryGetIndex(GridCell cell, out int index)
        {
            if (cell.X < 0 || cell.X >= dimensions.Width
                || cell.Z < 0 || cell.Z >= dimensions.Depth
                || cell.Y < 0 || cell.Y >= dimensions.Height)
            {
                index = -1;
                return false;
            }

            index = cell.X
                + (cell.Z * dimensions.Width)
                + (cell.Y * dimensions.Width * dimensions.Depth);
            return true;
        }
    }
}
