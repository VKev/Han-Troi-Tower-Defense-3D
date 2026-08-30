#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using TowerDefense3D.GridPlacement;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.GridPlacement.Editor
{
    /// <summary>
    /// Derives an enemy route for every spawn on a board so that the path enemies walk matches
    /// the road drawn on the map.
    ///
    /// Exit arrows can only describe one way out of a cell, so a loop drawn on the board is cut
    /// past rather than walked, and its cells sit on the map unused. A route is an ordered walk,
    /// which can enter a loop, come back to the junction and carry on.
    ///
    /// The walk prefers whichever neighbouring cell is furthest from the exit among those it has
    /// not used yet. That sends it around loops and down spurs first and leaves the run to the
    /// exit for last. Where two parallel branches join the same two junctions only one of them
    /// can be walked without doubling back; the generator reports what it could not cover rather
    /// than pretending otherwise.
    /// </summary>
    public static class BoardRouteGenerator
    {
        private const char NewLine = '\n';

        private const string BoardFolder = "Assets/Config/GridPlacement";

        private static readonly Vector2Int[] Offsets =
        {
            new Vector2Int(1, 0),
            new Vector2Int(0, -1),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1)
        };

        [MenuItem("Tools/Tower Defense/Generate Board Routes")]
        public static void GenerateRoutes()
        {
            string[] guids = AssetDatabase.FindAssets("t:BoardDefinition", new[] { BoardFolder });
            var report = new StringBuilder("Board route generation\n");
            int written = 0;

            for (int index = 0; index < guids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                var board = AssetDatabase.LoadAssetAtPath<BoardDefinition>(path);
                if (board == null)
                {
                    continue;
                }

                var road = new Dictionary<Vector2Int, bool>();
                var spawns = new List<Vector2Int>();
                var ends = new List<Vector2Int>();
                int level = ReadRoad(board, road, spawns, ends);
                if (spawns.Count == 0 || ends.Count == 0)
                {
                    continue;
                }

                List<Vector2Int> targets = AssignExits(road, spawns, ends);
                var routes = new List<List<Vector2Int>>();
                var covered = new Dictionary<Vector2Int, bool>();
                for (int spawn = 0; spawn < spawns.Count; spawn++)
                {
                    List<Vector2Int> walk = BuildWalk(road, spawns[spawn], targets[spawn]);
                    routes.Add(walk);
                    for (int step = 0; step < walk.Count; step++)
                    {
                        covered[walk[step]] = true;
                    }
                }

                bool everyRouteExits = true;
                for (int route = 0; route < routes.Count; route++)
                {
                    List<Vector2Int> walk = routes[route];
                    everyRouteExits &= ends.Contains(walk[walk.Count - 1]);
                }

                int arrowCovered = CountArrowCoverage(board, road, spawns);
                report.Append(System.IO.Path.GetFileNameWithoutExtension(path))
                    .Append(": arrows walk ")
                    .Append(arrowCovered)
                    .Append('/')
                    .Append(road.Count)
                    .Append(", generated routes walk ")
                    .Append(covered.Count)
                    .Append('/')
                    .Append(road.Count);

                // Routes take over from the exit arrows only when the arrows leave part of the
                // drawn road unused AND the generated walk covers all of it and finishes on a
                // Road End. A partial walk would be a downgrade: it would replace a path that
                // works with one that strands enemies part way.
                if (arrowCovered >= road.Count)
                {
                    report.Append("  [left on arrows: nothing to fix]").Append(NewLine);
                    ClearRoutes(board);
                    continue;
                }

                if (!everyRouteExits || covered.Count < road.Count)
                {
                    report.Append(everyRouteExits
                            ? "  [SKIPPED: cannot walk the whole road without doubling back]"
                            : "  [SKIPPED: a route does not finish on a Road End]")
                        .Append(NewLine)
                        .Append(Render(road, covered, spawns, ends));
                    ClearRoutes(board);
                    continue;
                }

                WriteRoutes(board, routes, level);
                written++;
                report.Append("  [routes written]").Append(NewLine)
                    .Append(Render(road, covered, spawns, ends));
            }

            AssetDatabase.SaveAssets();
            Debug.Log(report.Append("Boards written: ").Append(written).ToString());
        }

        /// <summary>
        /// How much of the drawn road the exit arrows actually walk, which is what a board does
        /// today. Anything they miss is paint the player sees but enemies never set foot on.
        /// </summary>
        private static int CountArrowCoverage(
            BoardDefinition board,
            Dictionary<Vector2Int, bool> road,
            List<Vector2Int> spawns)
        {
            var directions = new Dictionary<Vector2Int, RoadExitDirection>();
            var exits = new Dictionary<Vector2Int, bool>();
            IReadOnlyList<BoardCellDefinition> cells = board.Cells;
            for (int index = 0; index < cells.Count; index++)
            {
                BoardCellDefinition cell = cells[index];
                var key = new Vector2Int(cell.Coordinate.X, cell.Coordinate.Z);
                if (cell.RoadExitDirection != RoadExitDirection.None)
                {
                    directions[key] = cell.RoadExitDirection;
                }

                if (cell.IsRoadEnd)
                {
                    exits[key] = true;
                }
            }

            var covered = new Dictionary<Vector2Int, bool>();
            for (int spawn = 0; spawn < spawns.Count; spawn++)
            {
                Vector2Int current = spawns[spawn];
                covered[current] = true;
                for (int guard = 0; guard <= road.Count; guard++)
                {
                    if (exits.ContainsKey(current)
                        || !directions.TryGetValue(current, out RoadExitDirection direction))
                    {
                        break;
                    }

                    Vector2Int next = current + ToOffset(direction);
                    if (!road.ContainsKey(next))
                    {
                        break;
                    }

                    current = next;
                    covered[current] = true;
                }
            }

            return covered.Count;
        }

        private static Vector2Int ToOffset(RoadExitDirection direction)
        {
            switch (direction)
            {
                case RoadExitDirection.East:
                    return new Vector2Int(1, 0);
                case RoadExitDirection.South:
                    return new Vector2Int(0, -1);
                case RoadExitDirection.West:
                    return new Vector2Int(-1, 0);
                case RoadExitDirection.North:
                    return new Vector2Int(0, 1);
                default:
                    return Vector2Int.zero;
            }
        }

        private static void ClearRoutes(BoardDefinition board)
        {
            var serialized = new SerializedObject(board);
            SerializedProperty array = serialized.FindProperty("routes");
            if (array.arraySize == 0)
            {
                return;
            }

            array.arraySize = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(board);
        }

        private static int ReadRoad(
            BoardDefinition board,
            Dictionary<Vector2Int, bool> road,
            List<Vector2Int> spawns,
            List<Vector2Int> ends)
        {
            int level = 0;
            IReadOnlyList<BoardCellDefinition> cells = board.Cells;
            for (int index = 0; index < cells.Count; index++)
            {
                BoardCellDefinition cell = cells[index];
                if (!cell.IsRoad && !cell.IsRoadSpawn && !cell.IsRoadEnd)
                {
                    continue;
                }

                var key = new Vector2Int(cell.Coordinate.X, cell.Coordinate.Z);
                road[key] = true;
                level = cell.Coordinate.Y;
                if (cell.IsRoadSpawn)
                {
                    spawns.Add(key);
                }

                if (cell.IsRoadEnd)
                {
                    ends.Add(key);
                }
            }

            return level;
        }

        /// <summary>
        /// Pairs each spawn with its nearest exit, and avoids reusing an exit while another is
        /// still free, so a board drawn with one exit per spawn keeps them paired.
        /// </summary>
        private static List<Vector2Int> AssignExits(
            Dictionary<Vector2Int, bool> road,
            List<Vector2Int> spawns,
            List<Vector2Int> ends)
        {
            var distancesByExit = new List<Dictionary<Vector2Int, int>>();
            for (int exit = 0; exit < ends.Count; exit++)
            {
                distancesByExit.Add(BuildDistances(road, ends[exit]));
            }

            var targets = new List<Vector2Int>();
            var taken = new Dictionary<Vector2Int, bool>();
            for (int spawn = 0; spawn < spawns.Count; spawn++)
            {
                Vector2Int best = ends[0];
                int bestDistance = int.MaxValue;
                bool preferFree = taken.Count < ends.Count;
                for (int exit = 0; exit < ends.Count; exit++)
                {
                    if (preferFree && taken.ContainsKey(ends[exit]))
                    {
                        continue;
                    }

                    if (distancesByExit[exit].TryGetValue(spawns[spawn], out int distance)
                        && distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = ends[exit];
                    }
                }

                taken[best] = true;
                targets.Add(best);
            }

            return targets;
        }

        private static List<Vector2Int> BuildWalk(
            Dictionary<Vector2Int, bool> road,
            Vector2Int spawn,
            Vector2Int exit)
        {
            var walk = new List<Vector2Int> { spawn };
            var visited = new Dictionary<Vector2Int, bool> { { spawn, true } };
            Dictionary<Vector2Int, int> distance = BuildDistances(road, exit);
            Vector2Int current = spawn;
            Vector2Int previous = spawn;

            for (int guard = 0; guard < road.Count * 6; guard++)
            {
                if (current == exit && CountUnvisited(road, visited, current) == 0)
                {
                    break;
                }

                if (!TryStepToUnvisited(road, visited, distance, current, out Vector2Int next)
                    && !TryStepTowardsExit(road, distance, current, previous, out next))
                {
                    break;
                }

                previous = current;
                current = next;
                visited[current] = true;
                walk.Add(current);
            }

            return walk;
        }

        private static bool TryStepToUnvisited(
            Dictionary<Vector2Int, bool> road,
            Dictionary<Vector2Int, bool> visited,
            Dictionary<Vector2Int, int> distance,
            Vector2Int current,
            out Vector2Int next)
        {
            next = current;
            int furthest = -1;
            for (int index = 0; index < Offsets.Length; index++)
            {
                Vector2Int candidate = current + Offsets[index];
                if (!road.ContainsKey(candidate) || visited.ContainsKey(candidate))
                {
                    continue;
                }

                int score = distance.TryGetValue(candidate, out int value) ? value : 0;
                if (score > furthest)
                {
                    furthest = score;
                    next = candidate;
                }
            }

            return furthest >= 0;
        }

        private static bool TryStepTowardsExit(
            Dictionary<Vector2Int, bool> road,
            Dictionary<Vector2Int, int> distance,
            Vector2Int current,
            Vector2Int previous,
            out Vector2Int next)
        {
            next = current;
            int best = distance.TryGetValue(current, out int own) ? own : int.MaxValue;
            bool stepped = false;
            for (int index = 0; index < Offsets.Length; index++)
            {
                Vector2Int candidate = current + Offsets[index];
                if (!road.ContainsKey(candidate) || candidate == previous)
                {
                    continue;
                }

                if (distance.TryGetValue(candidate, out int value) && value < best)
                {
                    best = value;
                    next = candidate;
                    stepped = true;
                }
            }

            return stepped;
        }

        private static int CountUnvisited(
            Dictionary<Vector2Int, bool> road,
            Dictionary<Vector2Int, bool> visited,
            Vector2Int from)
        {
            var seen = new Dictionary<Vector2Int, bool> { { from, true } };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(from);
            int count = 0;
            while (queue.Count > 0)
            {
                Vector2Int cell = queue.Dequeue();
                for (int index = 0; index < Offsets.Length; index++)
                {
                    Vector2Int next = cell + Offsets[index];
                    if (!road.ContainsKey(next) || seen.ContainsKey(next))
                    {
                        continue;
                    }

                    seen[next] = true;
                    if (!visited.ContainsKey(next))
                    {
                        count++;
                    }

                    queue.Enqueue(next);
                }
            }

            return count;
        }

        private static Dictionary<Vector2Int, int> BuildDistances(
            Dictionary<Vector2Int, bool> road,
            Vector2Int target)
        {
            var distance = new Dictionary<Vector2Int, int> { { target, 0 } };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(target);
            while (queue.Count > 0)
            {
                Vector2Int cell = queue.Dequeue();
                int next = distance[cell] + 1;
                for (int index = 0; index < Offsets.Length; index++)
                {
                    Vector2Int neighbour = cell + Offsets[index];
                    if (road.ContainsKey(neighbour) && !distance.ContainsKey(neighbour))
                    {
                        distance[neighbour] = next;
                        queue.Enqueue(neighbour);
                    }
                }
            }

            return distance;
        }

        private static string Render(
            Dictionary<Vector2Int, bool> road,
            Dictionary<Vector2Int, bool> covered,
            List<Vector2Int> spawns,
            List<Vector2Int> ends)
        {
            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int minZ = int.MaxValue;
            int maxZ = int.MinValue;
            foreach (KeyValuePair<Vector2Int, bool> pair in road)
            {
                minX = Mathf.Min(minX, pair.Key.x);
                maxX = Mathf.Max(maxX, pair.Key.x);
                minZ = Mathf.Min(minZ, pair.Key.y);
                maxZ = Mathf.Max(maxZ, pair.Key.y);
            }

            var builder = new StringBuilder();
            for (int z = maxZ; z >= minZ; z--)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    var cell = new Vector2Int(x, z);
                    if (!road.ContainsKey(cell))
                    {
                        builder.Append('.');
                    }
                    else if (spawns.Contains(cell))
                    {
                        builder.Append('S');
                    }
                    else if (ends.Contains(cell))
                    {
                        builder.Append('E');
                    }
                    else
                    {
                        builder.Append(covered.ContainsKey(cell) ? '#' : '!');
                    }
                }

                builder.Append('\n');
            }

            return builder.ToString();
        }

        private static void WriteRoutes(
            BoardDefinition board,
            List<List<Vector2Int>> routes,
            int level)
        {
            var serialized = new SerializedObject(board);
            SerializedProperty array = serialized.FindProperty("routes");
            array.arraySize = routes.Count;
            for (int route = 0; route < routes.Count; route++)
            {
                SerializedProperty cells = array
                    .GetArrayElementAtIndex(route)
                    .FindPropertyRelative("cells");
                cells.arraySize = routes[route].Count;
                for (int step = 0; step < routes[route].Count; step++)
                {
                    SerializedProperty element = cells.GetArrayElementAtIndex(step);
                    element.FindPropertyRelative("x").intValue = routes[route][step].x;
                    element.FindPropertyRelative("z").intValue = routes[route][step].y;
                    element.FindPropertyRelative("y").intValue = level;
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(board);
        }
    }
}
#endif
