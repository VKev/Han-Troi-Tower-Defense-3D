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
        public event Action<ElementReactionEvent> ReactionTriggered;

        public void Step()
        {
            long tick = towerNetworkManager.CurrentTick;
            ApplySpawns(timeline.GetSpawns(tick));
            ApplyFrames(timeline.GetFrames(tick));
            PublishImpacts(timeline.GetImpacts(tick));
            PublishReactions(timeline.GetReactions(tick));
        }

        public void Reset()
        {
            timeline = new CombatTimeline();
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
                enemySystem.ApplyPlannedFrame(frames[index]);
            }
        }

        private void PublishImpacts(IReadOnlyList<ProjectileImpactEvent> impacts)
        {
            for (int index = 0; index < impacts.Count; index++)
            {
                ProjectileImpacted?.Invoke(impacts[index]);
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
