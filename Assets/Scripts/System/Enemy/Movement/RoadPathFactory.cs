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
            return CreatePaths(boardSystem).Primary;
        }

        public static RoadPathSet CreatePaths(BoardSystem boardSystem)
        {
            if (boardSystem == null)
            {
                throw new ArgumentNullException(nameof(boardSystem));
            }

            var roadCells = new HashSet<GridCell>();
            var roadDirections = new Dictionary<GridCell, RoadExitDirection>();
            var spawns = new List<GridCell>();
            var ends = new HashSet<GridCell>();
            IReadOnlyList<BoardCellDefinition> cells = boardSystem.Definition.Cells;

            for (int index = 0; index < cells.Count; index++)
            {
                BoardCellDefinition cell = cells[index];
                if (!cell.IsRoad && !cell.IsRoadSpawn && !cell.IsRoadEnd)
                {
                    continue;
                }

                roadCells.Add(cell.Coordinate);
                if (cell.RoadExitDirection != RoadExitDirection.None)
                {
                    roadDirections[cell.Coordinate] = cell.RoadExitDirection;
                }
                if (cell.IsRoadSpawn)
                {
                    spawns.Add(cell.Coordinate);
                }

                if (cell.IsRoadEnd)
                {
                    ends.Add(cell.Coordinate);
                }
            }

            // An empty route is one the painter created but nobody has drawn yet. It is not a
            // broken board, so it is ignored rather than refused; if that leaves no drawn route
            // at all the board falls back to its exit arrows.
            IReadOnlyList<BoardRouteDefinition> authoredRoutes = boardSystem.Definition.Routes;
            if (CountDrawnRoutes(authoredRoutes) > 0)
            {
                return CreateAuthoredRoutes(boardSystem, authoredRoutes, roadCells);
            }

            if (spawns.Count == 0 || ends.Count == 0)
            {
                throw new InvalidOperationException(
                    "Board road requires at least one RoadSpawn and one RoadEnd cell.");
            }

            var paths = new RoadPath[spawns.Count];
            for (int routeIndex = 0; routeIndex < spawns.Count; routeIndex++)
            {
                IReadOnlyList<GridCell> orderedCells = roadDirections.Count > 0
                    ? FollowAuthoredPath(roadCells, roadDirections, spawns[routeIndex], ends)
                    : FindPath(roadCells, spawns[routeIndex], ends);
                var worldPoints = new Vector3[orderedCells.Count];
                for (int pointIndex = 0; pointIndex < orderedCells.Count; pointIndex++)
                {
                    worldPoints[pointIndex] = boardSystem.Board.Mapper.CellToWorldCenter(
                        orderedCells[pointIndex]);
                }

                paths[routeIndex] = new RoadPath(worldPoints);
            }

            return new RoadPathSet(paths);
        }

        private static int CountDrawnRoutes(IReadOnlyList<BoardRouteDefinition> routes)
        {
            int count = 0;
            for (int index = 0; index < routes.Count; index++)
            {
                if (routes[index].Cells.Count > 0)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// An authored route is walked exactly as drawn, so it can lap a closed loop or leave a
        /// junction differently from another route. Only adjacency is enforced; repeating a cell
        /// is what makes a lap, so it is not an error.
        /// </summary>
        private static RoadPathSet CreateAuthoredRoutes(
            BoardSystem boardSystem,
            IReadOnlyList<BoardRouteDefinition> routes,
            ISet<GridCell> roadCells)
        {
            var paths = new RoadPath[CountDrawnRoutes(routes)];
            var weights = new int[paths.Length];
            var starts = new GridCell[paths.Length];
            int pathIndex = 0;
            for (int routeIndex = 0; routeIndex < routes.Count; routeIndex++)
            {
                IReadOnlyList<GridCell> cells = routes[routeIndex].Cells;
                if (cells.Count == 0)
                {
                    continue;
                }

                if (cells.Count < 2)
                {
                    throw new InvalidOperationException(
                        $"Authored route {routeIndex} needs at least two cells.");
                }

                var worldPoints = new Vector3[cells.Count];
                for (int cellIndex = 0; cellIndex < cells.Count; cellIndex++)
                {
                    GridCell cell = cells[cellIndex];
                    if (!roadCells.Contains(cell))
                    {
                        throw new InvalidOperationException(
                            $"Authored route {routeIndex} steps onto non-road cell {cell}.");
                    }

                    if (cellIndex > 0 && !IsAdjacent(cells[cellIndex - 1], cell))
                    {
                        throw new InvalidOperationException(
                            $"Authored route {routeIndex} jumps from {cells[cellIndex - 1]} "
                            + $"to {cell}, which do not share an edge.");
                    }

                    worldPoints[cellIndex] = boardSystem.Board.Mapper.CellToWorldCenter(cell);
                }

                paths[pathIndex++] = new RoadPath(worldPoints);
                weights[pathIndex - 1] = routes[routeIndex].Weight;
                starts[pathIndex - 1] = cells[0];
            }

            var orderedSpawns = new List<GridCell>();
            for (int index = 0; index < starts.Length; index++)
            {
                if (!Contains(orderedSpawns, starts[index]))
                {
                    orderedSpawns.Add(starts[index]);
                }
            }

            orderedSpawns.Sort(CompareCells);
            var spawnIndices = new int[starts.Length];
            for (int index = 0; index < starts.Length; index++)
            {
                spawnIndices[index] = orderedSpawns.IndexOf(starts[index]);
            }

            return new RoadPathSet(paths, weights, spawnIndices);
        }

        private static bool Contains(IReadOnlyList<GridCell> cells, GridCell target)
        {
            for (int index = 0; index < cells.Count; index++)
            {
                if (cells[index] == target)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CompareCells(GridCell left, GridCell right)
        {
            int x = left.X.CompareTo(right.X);
            if (x != 0)
            {
                return x;
            }

            int z = left.Z.CompareTo(right.Z);
            return z != 0 ? z : left.Y.CompareTo(right.Y);
        }

        private static bool IsAdjacent(GridCell left, GridCell right)
        {
            return left.Y == right.Y
                && Math.Abs(left.X - right.X) + Math.Abs(left.Z - right.Z) == 1;
        }

        private static IReadOnlyList<GridCell> FindPath(
            ISet<GridCell> roadCells,
            GridCell spawn,
            ISet<GridCell> ends)
        {
            var frontier = new Queue<GridCell>();
            var previous = new Dictionary<GridCell, GridCell>();
            var visited = new HashSet<GridCell> { spawn };
            frontier.Enqueue(spawn);

            while (frontier.Count > 0)
            {
                GridCell current = frontier.Dequeue();
                if (ends.Contains(current))
                {
                    return Reconstruct(previous, spawn, current);
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

        private static IReadOnlyList<GridCell> FollowAuthoredPath(
            ISet<GridCell> roadCells,
            IReadOnlyDictionary<GridCell, RoadExitDirection> directions,
            GridCell spawn,
            ISet<GridCell> ends)
        {
            var ordered = new List<GridCell> { spawn };
            var visited = new HashSet<GridCell> { spawn };
            GridCell current = spawn;

            while (!ends.Contains(current))
            {
                if (!directions.TryGetValue(current, out RoadExitDirection direction))
                {
                    throw new InvalidOperationException(
                        $"Road cell {current} needs an authored exit direction to reach RoadEnd.");
                }

                GridCell next = GetNeighbor(current, direction);
                if (!roadCells.Contains(next))
                {
                    throw new InvalidOperationException(
                        $"Road exit at {current} points to non-road cell {next}.");
                }

                if (!visited.Add(next))
                {
                    throw new InvalidOperationException(
                        $"Authored road directions contain a loop at {next}.");
                }

                ordered.Add(next);
                current = next;
            }

            return ordered;
        }

        private static GridCell GetNeighbor(GridCell coordinate, RoadExitDirection direction)
        {
            return direction switch
            {
                RoadExitDirection.East => new GridCell(
                    coordinate.X + 1,
                    coordinate.Z,
                    coordinate.Y),
                RoadExitDirection.South => new GridCell(
                    coordinate.X,
                    coordinate.Z - 1,
                    coordinate.Y),
                RoadExitDirection.West => new GridCell(
                    coordinate.X - 1,
                    coordinate.Z,
                    coordinate.Y),
                RoadExitDirection.North => new GridCell(
                    coordinate.X,
                    coordinate.Z + 1,
                    coordinate.Y),
                _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
            };
        }
    }
}
