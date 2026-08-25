using System;
using System.Collections.Generic;
using TowerDefense3D.GridPlacement;
using UnityEngine;

namespace TowerDefense3D.Enemies
{
    public static class RoadPathFactory
    {
        private static readonly (int X, int Z)[] NeighborOffsets =
        {
            (1, 0),
            (0, -1),
            (-1, 0),
            (0, 1)
        };

        public static RoadPath Create(BoardSystem boardSystem)
        {
            if (boardSystem == null)
            {
                throw new ArgumentNullException(nameof(boardSystem));
            }

            var roadCells = new HashSet<GridCell>();
            GridCell? spawn = null;
            GridCell? end = null;
            IReadOnlyList<BoardCellDefinition> cells = boardSystem.Definition.Cells;

            for (int index = 0; index < cells.Count; index++)
            {
                BoardCellDefinition cell = cells[index];
                if (!cell.IsRoad && !cell.IsRoadSpawn && !cell.IsRoadEnd)
                {
                    continue;
                }

                roadCells.Add(cell.Coordinate);
                if (cell.IsRoadSpawn)
                {
                    SetUniqueEndpoint(ref spawn, cell.Coordinate, "RoadSpawn");
                }

                if (cell.IsRoadEnd)
                {
                    SetUniqueEndpoint(ref end, cell.Coordinate, "RoadEnd");
                }
            }

            if (!spawn.HasValue || !end.HasValue)
            {
                throw new InvalidOperationException(
                    "Board road requires exactly one RoadSpawn and one RoadEnd cell.");
            }

            IReadOnlyList<GridCell> orderedCells = FindPath(roadCells, spawn.Value, end.Value);
            var worldPoints = new Vector3[orderedCells.Count];
            for (int index = 0; index < orderedCells.Count; index++)
            {
                worldPoints[index] = boardSystem.Board.Mapper.CellToWorldCenter(orderedCells[index]);
            }

            return new RoadPath(worldPoints);
        }

        private static void SetUniqueEndpoint(
            ref GridCell? endpoint,
            GridCell coordinate,
            string role)
        {
            if (endpoint.HasValue && endpoint.Value != coordinate)
            {
                throw new InvalidOperationException($"Board road contains more than one {role} cell.");
            }

            endpoint = coordinate;
        }

        private static IReadOnlyList<GridCell> FindPath(
            ISet<GridCell> roadCells,
            GridCell spawn,
            GridCell end)
        {
            var frontier = new Queue<GridCell>();
            var previous = new Dictionary<GridCell, GridCell>();
            var visited = new HashSet<GridCell> { spawn };
            frontier.Enqueue(spawn);

            while (frontier.Count > 0)
            {
                GridCell current = frontier.Dequeue();
                if (current == end)
                {
                    return Reconstruct(previous, spawn, end);
                }

                for (int index = 0; index < NeighborOffsets.Length; index++)
                {
                    (int xOffset, int zOffset) = NeighborOffsets[index];
                    var neighbor = new GridCell(
                        current.X + xOffset,
                        current.Z + zOffset,
                        current.Y);
                    if (!roadCells.Contains(neighbor) || !visited.Add(neighbor))
                    {
                        continue;
                    }

                    previous.Add(neighbor, current);
                    frontier.Enqueue(neighbor);
                }
            }

            throw new InvalidOperationException("RoadSpawn is not connected to RoadEnd.");
        }

        private static IReadOnlyList<GridCell> Reconstruct(
            IReadOnlyDictionary<GridCell, GridCell> previous,
            GridCell spawn,
            GridCell end)
        {
            var reversed = new List<GridCell> { end };
            GridCell current = end;
            while (current != spawn)
            {
                current = previous[current];
                reversed.Add(current);
            }

            reversed.Reverse();
            return reversed;
        }
    }
}
