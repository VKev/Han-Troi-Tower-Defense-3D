using System;
using System.Collections.Generic;
using TowerDefense3D.Towers;
using TowerDefense3D.Waves;

namespace TowerDefense3D.Enemies
{
    public sealed class CombatTimelineSystem : IDisposable
    {
        private readonly TowerNetworkManager towerNetworkManager;
        private readonly EnemySystem enemySystem;
        private readonly WaveSystem waveSystem;
        private readonly CombatTimelinePlanner planner;
        private CombatTimeline timeline = new CombatTimeline();
        private long heldFrameRequestEnemyId;
        private PlannedEnemyFrame? heldEnemyFrame;
        private bool isDisposed;

        public CombatTimelineSystem(
            TowerNetworkManager towerNetworkManager,
            EnemySystem enemySystem,
            WaveSystem waveSystem,
            CombatTimelinePlanner planner)
        {
            this.towerNetworkManager = towerNetworkManager
                ?? throw new ArgumentNullException(nameof(towerNetworkManager));
            this.enemySystem = enemySystem ?? throw new ArgumentNullException(nameof(enemySystem));
            this.waveSystem = waveSystem ?? throw new ArgumentNullException(nameof(waveSystem));
            this.planner = planner ?? throw new ArgumentNullException(nameof(planner));
            waveSystem.WavePlanCreated += HandleWavePlanCreated;
        }

        internal event Action<ProjectileImpactEvent> ProjectileImpacted;
        internal event Action<FireHitEvent> FireHitResolved;
        public event Action<ElementReactionEvent> ReactionTriggered;
        public event Action<HeroAttackEvent> HeroAttackStarted;

        public void Step()
        {
            long tick = towerNetworkManager.CurrentTick;
            ApplySpawns(timeline.GetSpawns(tick));
            PublishHeroAttacks(timeline.GetHeroAttacks(tick));
            PublishFireHits(timeline.GetFireHits(tick));
            PublishImpacts(timeline.GetImpacts(tick));
            PublishReactions(timeline.GetReactions(tick));
            ApplyFrames(timeline.GetFrames(tick));
        }

        public void Reset()
        {
            timeline = new CombatTimeline();
            heldFrameRequestEnemyId = 0L;
            heldEnemyFrame = null;
        }

        internal void HoldLethalFrame(long enemyId)
        {
            if (enemyId > 0L && !heldEnemyFrame.HasValue)
            {
                heldFrameRequestEnemyId = enemyId;
            }
        }

        internal void ReleaseHeldEnemyFrame()
        {
            if (!heldEnemyFrame.HasValue)
            {
                heldFrameRequestEnemyId = 0L;
                return;
            }

            PlannedEnemyFrame frame = heldEnemyFrame.Value;
            heldEnemyFrame = null;
            heldFrameRequestEnemyId = 0L;
            enemySystem.ApplyPlannedFrame(frame);
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            waveSystem.WavePlanCreated -= HandleWavePlanCreated;
            Reset();
        }

        private void HandleWavePlanCreated(IReadOnlyList<WaveSpawnOrder> plan)
        {
            timeline = planner.Create(plan);
        }

        private void ApplySpawns(IReadOnlyList<PlannedEnemySpawn> spawns)
        {
            for (int index = 0; index < spawns.Count; index++)
            {
                enemySystem.SpawnPlannedSummon(spawns[index]);
            }
        }

        private void ApplyFrames(IReadOnlyList<PlannedEnemyFrame> frames)
        {
            for (int index = 0; index < frames.Count; index++)
            {
                PlannedEnemyFrame frame = frames[index];
                if (frame.EnemyId == heldFrameRequestEnemyId
                    && frame.Removal == PlannedEnemyRemoval.Killed)
                {
                    heldEnemyFrame = frame;
                    heldFrameRequestEnemyId = 0L;
                    enemySystem.ApplyPlannedFrame(frame.HoldAlive());
                    continue;
                }

                enemySystem.ApplyPlannedFrame(frame);
            }

            heldFrameRequestEnemyId = 0L;
        }

        private void PublishFireHits(IReadOnlyList<FireHitEvent> hits)
        {
            for (int index = 0; index < hits.Count; index++)
            {
                FireHitResolved?.Invoke(hits[index]);
            }
        }

        private void PublishImpacts(IReadOnlyList<ProjectileImpactEvent> impacts)
        {
            for (int index = 0; index < impacts.Count; index++)
            {
                ProjectileImpacted?.Invoke(impacts[index]);
            }
        }

        private void PublishHeroAttacks(IReadOnlyList<HeroAttackEvent> attacks)
        {
            for (int index = 0; index < attacks.Count; index++)
            {
                HeroAttackStarted?.Invoke(attacks[index]);
            }
        }

        private void PublishReactions(IReadOnlyList<PlannedReactionEvent> reactions)
        {
            for (int index = 0; index < reactions.Count; index++)
            {
                PlannedReactionEvent reaction = reactions[index];
                ReactionTriggered?.Invoke(new ElementReactionEvent(
                    reaction.EnemyId,
                    reaction.ReactionId,
                    reaction.Pair,
                    reaction.Position,
                    reaction.DurationSeconds));
            }
        }
    }
}
