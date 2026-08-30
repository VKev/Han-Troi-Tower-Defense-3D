using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.Enemies
{
    /// <summary>
    /// Immutable routes authored for one board. Wave enemies rotate through routes by their
    /// deterministic enemy id; boss summons retain their parent's route.
    ///
    /// Each route is widened into parallel lanes so a queue of enemies walking the same road
    /// spreads out instead of stacking into one line. Overlap is reduced, not forbidden: enemies
    /// still pass through each other, and lanes converge again wherever the road turns.
    ///
    /// The lane an enemy walks is derived from its id rather than drawn from a random source,
    /// because the combat timeline is precomputed and replayed: the planner and the live
    /// simulation have to reach the same answer without sharing state.
    /// </summary>
    public sealed class RoadPathSet
    {
        public const int LaneCount = 3;
        public const int CenterLaneIndex = 1;

        /// <summary>
        /// Spacing between neighbouring lanes, in meters. Deliberately tight: the goal is for a
        /// column of enemies to look like it wanders rather than to read as three marked lanes,
        /// so the offset only has to break the single-file line, not separate it.
        /// </summary>
        private const float LaneOffsetMeters = 0.18f;

        private readonly RoadPath[] paths;
        private readonly RoadPath[][] lanesByRoute;

        public RoadPathSet(IReadOnlyList<RoadPath> paths)
        {
            if (paths == null || paths.Count == 0)
            {
                throw new ArgumentException("At least one road path is required.", nameof(paths));
            }

            this.paths = new RoadPath[paths.Count];
            lanesByRoute = new RoadPath[paths.Count][];
            for (int index = 0; index < paths.Count; index++)
            {
                this.paths[index] = paths[index]
                    ?? throw new ArgumentException("Road paths cannot contain null.", nameof(paths));
                lanesByRoute[index] = BuildLanes(this.paths[index]);
            }
        }

        public int Count => paths.Length;
        public RoadPath Primary => paths[0];

        public int GetRouteIndex(long enemyId)
        {
            if (enemyId <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(enemyId));
            }

            return (int)((enemyId - 1L) % paths.Length);
        }

        /// <summary>
        /// Bosses always walk the middle lane, so the one enemy the player tracks stays on the
        /// line the road was drawn along. Everything else is scattered by a hash of its id, which
        /// looks arbitrary in play yet replays identically.
        /// </summary>
        public int GetLaneIndex(long enemyId, EnemyDefinition definition)
        {
            if (definition != null && definition.Rank == EnemyRank.Boss)
            {
                return CenterLaneIndex;
            }

            return (int)(Scatter(enemyId) % LaneCount);
        }

        public RoadPath Get(int routeIndex)
        {
            if (routeIndex < 0 || routeIndex >= paths.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(routeIndex));
            }

            return paths[routeIndex];
        }

        public RoadPath GetLane(int routeIndex, int laneIndex)
        {
            if (routeIndex < 0 || routeIndex >= paths.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(routeIndex));
            }

            if (laneIndex < 0 || laneIndex >= LaneCount)
            {
                throw new ArgumentOutOfRangeException(nameof(laneIndex));
            }

            return lanesByRoute[routeIndex][laneIndex];
        }

        public RoadPath GetForEnemy(long enemyId, EnemyDefinition definition)
        {
            return GetLane(GetRouteIndex(enemyId), GetLaneIndex(enemyId, definition));
        }

        /// <summary>
        /// Knuth's multiplicative hash. Consecutive ids land on different lanes instead of
        /// cycling in a visible 0,1,2 pattern the way a plain modulo would.
        /// </summary>
        private static uint Scatter(long enemyId)
        {
            unchecked
            {
                return (uint)enemyId * 2654435761u >> 13;
            }
        }

        private static RoadPath[] BuildLanes(RoadPath center)
        {
            var lanes = new RoadPath[LaneCount];
            for (int laneIndex = 0; laneIndex < LaneCount; laneIndex++)
            {
                float offset = (laneIndex - CenterLaneIndex) * LaneOffsetMeters;
                lanes[laneIndex] = offset == 0f ? center : BuildLane(center, offset);
            }

            return lanes;
        }

        private static RoadPath BuildLane(RoadPath center, float offsetMeters)
        {
            var points = new Vector3[center.PointCount];
            for (int index = 0; index < points.Length; index++)
            {
                Vector3 forward = GetSmoothedForward(center, index);
                var right = new Vector3(forward.z, 0f, -forward.x);
                points[index] = center.GetPoint(index) + right * offsetMeters;
            }

            return new RoadPath(points);
        }

        /// <summary>
        /// Averages the segments either side of a point so a corner is mitred instead of leaving
        /// the offset polyline with a step in it.
        /// </summary>
        private static Vector3 GetSmoothedForward(RoadPath center, int index)
        {
            Vector3 previous = center.GetPoint(Mathf.Max(0, index - 1));
            Vector3 next = center.GetPoint(Mathf.Min(center.PointCount - 1, index + 1));
            Vector3 forward = next - previous;
            forward.y = 0f;
            return forward.sqrMagnitude > 0f ? forward.normalized : Vector3.forward;
        }
    }
}
