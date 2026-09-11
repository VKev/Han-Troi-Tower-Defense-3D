using System;
using UnityEngine;

namespace TowerDefense3D.Enemies
{
    internal readonly struct FireHitEvent
    {
        public FireHitEvent(long enemyId)
        {
            if (enemyId <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(enemyId));
            }

            EnemyId = enemyId;
        }

        public long EnemyId { get; }
    }

    internal readonly struct ProjectileImpactEvent
    {
        public ProjectileImpactEvent(long projectileId, Vector3 position)
        {
            if (projectileId <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(projectileId));
            }

            ProjectileId = projectileId;
            Position = position;
        }

        public long ProjectileId { get; }
        public Vector3 Position { get; }
    }
}
