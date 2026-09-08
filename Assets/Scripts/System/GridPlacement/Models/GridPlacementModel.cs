using UnityEngine;

namespace TowerDefense3D.GridPlacement
{
    /// <summary>
    /// Owns placement rules and occupancy for one level-scoped board.
    /// </summary>
    internal sealed class GridPlacementModel
    {
        private readonly BoardDefinition boardDefinition;
        private readonly GridBoard board;
        private readonly GridOccupancy occupancy;
        private readonly PlacementValidator validator;
        private int nextOwnerId = 1;

        internal GridPlacementModel(BoardDefinition boardDefinition, GridBoard board)
        {
            this.boardDefinition = boardDefinition;
            this.board = board;
            occupancy = new GridOccupancy(boardDefinition.Dimensions);
            validator = new PlacementValidator(board, occupancy);
        }

        internal GridOccupancy Occupancy => occupancy;
        internal float CellSize => boardDefinition.CellSize;
        internal float HeightUnit => boardDefinition.HeightUnit;

        internal bool TryWorldToCell(Vector3 worldPoint, out GridCell cell)
        {
            return board.Mapper.TryWorldToCell(worldPoint, out cell);
        }

        internal PlacementResult Evaluate(GridCell cell, TowerFootprint footprint)
        {
            return validator.Evaluate(cell, footprint);
        }

        internal bool TryReserve(
            GridCell cell,
            TowerFootprint footprint,
            out PlacementReservation reservation)
        {
            return occupancy.TryReserve(cell, footprint, out reservation);
        }

        internal Vector3 GetFootprintBottomCenter(GridCell anchor, TowerFootprint footprint)
        {
            return board.Mapper.FootprintBottomCenter(anchor, footprint);
        }

        internal int NextOwnerId()
        {
            if (nextOwnerId == int.MaxValue)
            {
                nextOwnerId = 1;
            }

            return nextOwnerId++;
        }
    }
}
