using System;
using System.Collections.Generic;

namespace TowerDefense3D.Enemies
{
    public sealed class EnemyPresentationSystem : IDisposable
    {
        private readonly EnemySystem enemySystem;
        private readonly IEnemyViewPool viewPool;
        private readonly List<EnemySnapshot> snapshots = new List<EnemySnapshot>();
        private bool isStarted;

        public EnemyPresentationSystem(
            EnemySystem enemySystem,
            IEnemyViewPool viewPool)
        {
            this.enemySystem = enemySystem ?? throw new ArgumentNullException(nameof(enemySystem));
            this.viewPool = viewPool ?? throw new ArgumentNullException(nameof(viewPool));
        }

        public void Start()
        {
            viewPool.Initialize();
            enemySystem.EnemySpawned += HandleEnemySpawned;
            enemySystem.EnemyKilled += HandleEnemyRemoved;
            enemySystem.EnemyLeaked += HandleEnemyRemoved;
            isStarted = true;
        }

        public void LateTick(float interpolationAlpha)
        {
            enemySystem.CopySnapshotsTo(snapshots);
            viewPool.Render(snapshots, interpolationAlpha);
        }

        public void Dispose()
        {
            if (!isStarted)
            {
                return;
            }

            isStarted = false;
            enemySystem.EnemySpawned -= HandleEnemySpawned;
            enemySystem.EnemyKilled -= HandleEnemyRemoved;
            enemySystem.EnemyLeaked -= HandleEnemyRemoved;
            viewPool.ReleaseAll();
        }

        private void HandleEnemySpawned(EnemySnapshot enemy)
        {
            viewPool.Spawn(enemy);
        }

        private void HandleEnemyRemoved(EnemySnapshot enemy)
        {
            viewPool.Despawn(enemy.EnemyId);
        }
    }
}
