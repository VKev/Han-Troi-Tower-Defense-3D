using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.GridPlacement
{
    /// <summary>
    /// Finds the Road Spawns on the board - the cells enemies walk in from.
    ///
    /// A route's first cell is not the spawn: the painter usually starts drawing a cell or two
    /// inside the board, so the spawn sits further out than where any route begins. Anything that
    /// wants to head for a spawn has to read the cells, not the routes.
    ///
    /// This lives beside the board rather than with the road pathing that wants it: pathing is in
    /// the system layer, which cannot see scene components, so it takes the answer as plain points.
    /// </summary>
    public static class RoadSpawnLocator
    {
        public static List<Vector3> CollectWorldPositions()
        {
            var positions = new List<Vector3>();
            var boardView = Object.FindFirstObjectByType<BoardView>();
            if (boardView == null || boardView.Board == null)
            {
                return positions;
            }

            var boardSystem = new BoardSystem(boardView);
            IReadOnlyList<BoardCellDefinition> cells = boardSystem.Definition.Cells;
            for (int index = 0; index < cells.Count; index++)
            {
                if (cells[index].IsRoadSpawn)
                {
                    positions.Add(
                        boardSystem.Board.Mapper.CellToWorldCenter(cells[index].Coordinate));
                }
            }

            return positions;
        }
    }
}
