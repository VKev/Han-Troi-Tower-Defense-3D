using System;

namespace TowerDefense3D.Towers
{
    internal readonly struct TowerProjectileSpawnOrder
    {
        public TowerProjectileSpawnOrder(long spawnTick, TowerProjectileSnapshot projectile)
        {
            if (spawnTick <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(spawnTick));
            }

            SpawnTick = spawnTick;
            Projectile = projectile;
        }

        public long SpawnTick { get; }
        public TowerProjectileSnapshot Projectile { get; }
    }
}
