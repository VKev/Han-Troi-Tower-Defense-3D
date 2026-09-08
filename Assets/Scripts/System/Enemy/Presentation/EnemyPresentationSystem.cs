using System;
using System.Collections.Generic;

namespace TowerDefense3D.Enemies
{
    public sealed class EnemyPresentationSystem : IDisposable
    {
        private readonly EnemySystem enemySystem;
        private readonly CombatTimelineSystem combatTimelineSystem;
        private readonly IEnemyViewPool viewPool;
        private readonly List<EnemySnapshot> snapshots = new List<EnemySnapshot>();
        private bool isStarted;

        public EnemyPresentationSystem(
            EnemySystem enemySystem,
            CombatTimelineSystem combatTimelineSystem,
            IEnemyViewPool viewPool)
        {
            this.enemySystem = enemySystem ?? throw new ArgumentNullException(nameof(enemySystem));
            this.combatTimelineSystem = combatTimelineSystem
                ?? throw new ArgumentNullException(nameof(combatTimelineSystem));
            this.viewPool = viewPool ?? throw new ArgumentNullException(nameof(viewPool));
        }

        public void Start()
        {
            enemySystem.EnemySpawned += HandleEnemySpawned;
            enemySystem.EnemyKilled += HandleEnemyRemoved;
            enemySystem.EnemyLeaked += HandleEnemyRemoved;
            enemySystem.EnemyDespawned += HandleEnemyReleased;
            enemySystem.EnemyRekeyed += HandleEnemyRekeyed;
            combatTimelineSystem.ReactionTriggered += HandleReactionTriggered;
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
            enemySystem.EnemyDespawned -= HandleEnemyReleased;
            enemySystem.EnemyRekeyed -= HandleEnemyRekeyed;
            combatTimelineSystem.ReactionTriggered -= HandleReactionTriggered;
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

        /// <summary>
        /// Removed without dying, so it leaves without a death to watch.
        /// </summary>
        private void HandleEnemyReleased(EnemySnapshot enemy)
        {
            viewPool.ReleaseImmediate(enemy.EnemyId);
        }

        private void HandleEnemyRekeyed(long oldEnemyId, long newEnemyId)
        {
            viewPool.Rekey(oldEnemyId, newEnemyId);
        }

        private void HandleReactionTriggered(ElementReactionEvent reaction)
        {
            viewPool.ShowReaction(reaction.EnemyId, reaction);
        }
    }
}
