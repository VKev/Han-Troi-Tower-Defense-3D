using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.Enemies
{
    /// <summary>
    /// Plans the chain of landing points a jumper hops through to leave the board along a road,
    /// with every hop covering the same distance.
    ///
    /// One fixed jump animation has one length, so hops of mixed length make it read as a slide on
    /// the short ones. Landing points are therefore spaced at exactly the jump range rather than
    /// wherever the road cells fall.
    ///
    /// Every landing has to be on road, and that is what <see cref="FindShortestRoadPathOut"/> is
    /// for: it hands back a chain of neighbouring road tiles, so the line between any two
    /// consecutive points stays on road and any point sampled along it is a legal place to come
    /// down. The jumper still cuts corners: a hop's chord leaves the road and flies straight while
    /// the road bends. It only ever lands back on it.
    ///
    /// The range does not divide the road evenly, so the leftover at the end is simply not
    /// travelled - better a chain that stops on road than one padded with a short hop or a landing
    /// out in the sand.
    ///
    /// Distances are measured on the XZ plane so a jumper hovering above the board - or road
    /// authored at a different height - does not spend its reach climbing, and landings take the
    /// road's height rather than the jumper's.
    /// </summary>
    public static class RoadJumpPathPlanner
    {
        /// <summary>
        /// How far apart two road tiles may sit and still count as connected, in cells. Above one
        /// so a bend that steps diagonally still joins up, short enough that two roads running
        /// alongside each other stay separate.
        /// </summary>
        private const float TileLinkReachInCells = 1.6f;

        /// <summary>
        /// How close a road tile has to sit to a Road Spawn to count as that spawn's tile, in
        /// cells. Half a cell, so it is the tile the spawn stands on and no other.
        /// </summary>
        private const float SpawnMatchReachInCells = 0.5f;

        /// <summary>
        /// The shortest way off the board along the road, out through a Road Spawn.
        ///
        /// Routes are not consulted. A route is the line enemies walk in, which says nothing about
        /// the quickest way out, and following one sent the jumper down whichever branch the level
        /// happened to draw first. What it heads for instead is a spawn: that is where the road
        /// leaves the board, so it is where a jumper leaving the board should be going.
        ///
        /// So: Dijkstra across the road tiles from the one nearest the jumper, pick the spawn that
        /// comes out nearest along the road, then carry on past it - still on road, still on the
        /// same path - to the first tile that is out of shot.
        ///
        /// With no spawn reachable it simply heads for the nearest tile out of shot, and with
        /// nothing out of shot at all it goes as far as the road reaches. A board that has neither
        /// still gets an escape rather than a frog standing still.
        /// </summary>
        public static List<Vector3> FindShortestRoadPathOut(
            IReadOnlyList<Vector3> roadTiles,
            IReadOnlyList<Vector3> spawnPositions,
            Vector3 from,
            Func<Vector3, bool> isOffScreen)
        {
            if (roadTiles == null)
            {
                throw new ArgumentNullException(nameof(roadTiles));
            }

            if (isOffScreen == null)
            {
                throw new ArgumentNullException(nameof(isOffScreen));
            }

            var path = new List<Vector3>();
            if (roadTiles.Count == 0)
            {
                return path;
            }

            float cellSize = EstimateCellSize(roadTiles);
            int start = FindNearestTile(roadTiles, from);
            int[] previous = FindShortestPaths(
                roadTiles,
                start,
                cellSize * TileLinkReachInCells,
                out float[] best);

            int spawn = FindNearestSpawnTile(
                roadTiles,
                spawnPositions,
                best,
                cellSize * SpawnMatchReachInCells);
            int goal = FindGoal(roadTiles, previous, best, spawn, isOffScreen);
            for (int node = goal; node >= 0; node = previous[node])
            {
                path.Add(roadTiles[node]);
            }

            path.Reverse();
            return path;
        }

        /// <summary>
        /// The spawn that comes out nearest along the road, as the index of the tile it stands on.
        /// Negative when the road reaches no spawn at all.
        /// </summary>
        private static int FindNearestSpawnTile(
            IReadOnlyList<Vector3> roadTiles,
            IReadOnlyList<Vector3> spawnPositions,
            IReadOnlyList<float> best,
            float matchReach)
        {
            if (spawnPositions == null)
            {
                return -1;
            }

            int nearest = -1;
            float nearestCost = float.PositiveInfinity;
            for (int index = 0; index < roadTiles.Count; index++)
            {
                if (float.IsPositiveInfinity(best[index]) || best[index] >= nearestCost)
                {
                    continue;
                }

                for (int spawn = 0; spawn < spawnPositions.Count; spawn++)
                {
                    if (DistanceOnGround(roadTiles[index], spawnPositions[spawn]) <= matchReach)
                    {
                        nearest = index;
                        nearestCost = best[index];
                        break;
                    }
                }
            }

            return nearest;
        }

        /// <summary>
        /// Where the path ends: the nearest tile out of shot, among those the jumper reaches by way
        /// of the spawn. Falls back to the furthest such tile when none of them is out of shot yet.
        /// </summary>
        private static int FindGoal(
            IReadOnlyList<Vector3> roadTiles,
            IReadOnlyList<int> previous,
            IReadOnlyList<float> best,
            int spawn,
            Func<Vector3, bool> isOffScreen)
        {
            int goal = -1;
            float goalCost = float.PositiveInfinity;
            int furthest = -1;
            float furthestCost = -1f;
            for (int index = 0; index < roadTiles.Count; index++)
            {
                if (float.IsPositiveInfinity(best[index]))
                {
                    continue;
                }

                if (spawn >= 0 && !PassesThrough(previous, index, spawn))
                {
                    continue;
                }

                if (best[index] > furthestCost)
                {
                    furthestCost = best[index];
                    furthest = index;
                }

                if (best[index] < goalCost && isOffScreen(roadTiles[index]))
                {
                    goalCost = best[index];
                    goal = index;
                }
            }

            if (goal >= 0)
            {
                return goal;
            }

            // Nothing out of shot beyond the spawn: stop at the spawn rather than at whatever tile
            // happens to be furthest, so the escape still reads as leaving by the spawn.
            return furthest >= 0 ? furthest : spawn;
        }

        /// <summary>Whether the shortest path to a tile runs through the given one.</summary>
        private static bool PassesThrough(IReadOnlyList<int> previous, int tile, int through)
        {
            for (int node = tile; node >= 0; node = previous[node])
            {
                if (node == through)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Dijkstra across the road tiles. Tile counts are a board's worth, so the dense form costs
        /// less than a heap would.
        /// </summary>
        private static int[] FindShortestPaths(
            IReadOnlyList<Vector3> roadTiles,
            int start,
            float reach,
            out float[] best)
        {
            int count = roadTiles.Count;
            best = new float[count];
            var previous = new int[count];
            var settled = new bool[count];
            for (int index = 0; index < count; index++)
            {
                best[index] = float.PositiveInfinity;
                previous[index] = -1;
            }

            best[start] = 0f;
            while (true)
            {
                int current = -1;
                float currentCost = float.PositiveInfinity;
                for (int index = 0; index < count; index++)
                {
                    if (!settled[index] && best[index] < currentCost)
                    {
                        current = index;
                        currentCost = best[index];
                    }
                }

                if (current < 0)
                {
                    return previous;
                }

                settled[current] = true;
                for (int next = 0; next < count; next++)
                {
                    if (settled[next] || next == current)
                    {
                        continue;
                    }

                    float step = DistanceOnGround(roadTiles[current], roadTiles[next]);
                    if (step > reach)
                    {
                        continue;
                    }

                    float candidate = currentCost + step;
                    if (candidate < best[next])
                    {
                        best[next] = candidate;
                        previous[next] = current;
                    }
                }
            }
        }

        /// <summary>
        /// The board's cell size, read off the tiles themselves: neighbouring tiles are one cell
        /// apart, so the closest pair of tiles anywhere on the board is exactly one cell.
        /// </summary>
        private static float EstimateCellSize(IReadOnlyList<Vector3> roadTiles)
        {
            float closest = float.PositiveInfinity;
            for (int index = 0; index < roadTiles.Count; index++)
            {
                for (int other = index + 1; other < roadTiles.Count; other++)
                {
                    float distance = DistanceOnGround(roadTiles[index], roadTiles[other]);
                    if (distance > 0f && distance < closest)
                    {
                        closest = distance;
                    }
                }
            }

            return float.IsPositiveInfinity(closest) ? 1f : closest;
        }

        private static int FindNearestTile(IReadOnlyList<Vector3> roadTiles, Vector3 from)
        {
            int nearest = 0;
            float nearestDistance = float.MaxValue;
            for (int index = 0; index < roadTiles.Count; index++)
            {
                float distance = DistanceOnGround(from, roadTiles[index]);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = index;
                }
            }

            return nearest;
        }

        /// <summary>
        /// The landing points to hop through, in order: each one exactly
        /// <paramref name="jumpDistanceMeters"/> from the last, and each one on the guide.
        ///
        /// The jumper's own position is only the distance the first hop is measured from; it is not
        /// a landing, and its height is not carried into the chain.
        /// </summary>
        public static List<Vector3> SpaceEvenly(
            Vector3 start,
            IReadOnlyList<Vector3> guide,
            float jumpDistanceMeters)
        {
            if (guide == null)
            {
                throw new ArgumentNullException(nameof(guide));
            }

            var landings = new List<Vector3>();
            if (guide.Count == 0 || jumpDistanceMeters <= 0f)
            {
                return landings;
            }

            var flatStart = new Vector3(start.x, guide[0].y, start.z);
            Vector3 previousLanding = flatStart;
            Vector3 cursor = guide[0];
            int segmentIndex = 1;

            // A jumper standing further from the road than one hop cannot reach it at the exact
            // range, so its first hop is whatever getting onto the road costs. Landing on road
            // matters more than that one step being uniform.
            if (DistanceOnGround(flatStart, cursor) >= jumpDistanceMeters)
            {
                landings.Add(cursor);
                previousLanding = cursor;
            }

            while (segmentIndex < guide.Count)
            {
                if (TryStepAlong(
                    cursor,
                    guide[segmentIndex],
                    previousLanding,
                    jumpDistanceMeters,
                    out Vector3 landing))
                {
                    landings.Add(landing);
                    previousLanding = landing;
                    cursor = landing;
                    continue;
                }

                cursor = guide[segmentIndex];
                segmentIndex++;
            }

            // Whatever road is left over at the end is shorter than a hop, so it is simply not
            // travelled: the chain stops on the last full-range landing that still fits on road
            // rather than taking one short step, or one step off the road, to reach the very end.
            if (landings.Count == 0)
            {
                landings.Add(guide[guide.Count - 1]);
            }

            return landings;
        }

        /// <summary>
        /// The point on the segment that sits exactly <paramref name="jumpDistanceMeters"/> from
        /// <paramref name="center"/>, or false when the whole segment stays within that range. The
        /// far root of the ray/circle intersection is the one wanted: the segment starts inside the
        /// range and this is where it leaves.
        /// </summary>
        private static bool TryStepAlong(
            Vector3 from,
            Vector3 to,
            Vector3 center,
            float jumpDistanceMeters,
            out Vector3 point)
        {
            point = default;
            float directionX = to.x - from.x;
            float directionZ = to.z - from.z;
            float a = directionX * directionX + directionZ * directionZ;
            if (a <= 0f)
            {
                return false;
            }

            float offsetX = from.x - center.x;
            float offsetZ = from.z - center.z;
            float b = 2f * (offsetX * directionX + offsetZ * directionZ);
            float c = offsetX * offsetX + offsetZ * offsetZ
                - jumpDistanceMeters * jumpDistanceMeters;
            float discriminant = b * b - 4f * a * c;
            if (discriminant < 0f)
            {
                return false;
            }

            float travel = (-b + Mathf.Sqrt(discriminant)) / (2f * a);
            if (travel <= 0f || travel > 1f)
            {
                return false;
            }

            point = new Vector3(
                from.x + directionX * travel,
                to.y,
                from.z + directionZ * travel);
            return true;
        }

        private static float DistanceOnGround(Vector3 from, Vector3 to)
        {
            float x = to.x - from.x;
            float z = to.z - from.z;
            return Mathf.Sqrt(x * x + z * z);
        }
    }
}
