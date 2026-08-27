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

        private void HandleReactionTriggered(ElementReactionEvent reaction)
        {
            viewPool.ShowReaction(reaction.EnemyId, reaction.Pair);
        }
    }
}
