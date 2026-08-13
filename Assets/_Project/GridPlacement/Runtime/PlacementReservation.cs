using System;

namespace TowerDefense3D.GridPlacement
{
    /// <summary>
    /// Owns one atomic occupancy reservation until committed or rolled back.
    /// </summary>
    public sealed class PlacementReservation : IDisposable
    {
        private GridOccupancy occupancy;
        private readonly GridCell[] cells;
        private readonly int reservedValue;

        internal PlacementReservation(GridOccupancy occupancy, GridCell[] cells, int reservedValue)
        {
            this.occupancy = occupancy;
            this.cells = cells;
            this.reservedValue = reservedValue;
        }

        public bool IsActive => occupancy != null;

        public bool Commit(int ownerId)
        {
            if (occupancy == null || !occupancy.Commit(cells, reservedValue, ownerId))
            {
                return false;
            }

            occupancy = null;
            return true;
        }

        public void Rollback()
        {
            if (occupancy == null)
            {
                return;
            }

            occupancy.Rollback(cells, reservedValue);
            occupancy = null;
        }

        public void Dispose() => Rollback();
    }
}
