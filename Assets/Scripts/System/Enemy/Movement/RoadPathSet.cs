using System;
using System.Collections.Generic;

namespace TowerDefense3D.Enemies
{
    /// <summary>
    /// Immutable routes authored for one board. Wave enemies rotate through routes by their
    /// deterministic enemy id; boss summons retain their parent's route.
    /// </summary>
    public sealed class RoadPathSet
    {
        private readonly RoadPath[] paths;

        public RoadPathSet(IReadOnlyList<RoadPath> paths)
        {
            if (paths == null || paths.Count == 0)
            {
                throw new ArgumentException("At least one road path is required.", nameof(paths));
            }

            this.paths = new RoadPath[paths.Count];
            for (int index = 0; index < paths.Count; index++)
            {
                this.paths[index] = paths[index]
                    ?? throw new ArgumentException("Road paths cannot contain null.", nameof(paths));
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

        public RoadPath Get(int routeIndex)
        {
            if (routeIndex < 0 || routeIndex >= paths.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(routeIndex));
            }

            return paths[routeIndex];
        }

        public RoadPath GetForEnemy(long enemyId) => Get(GetRouteIndex(enemyId));
    }
}
