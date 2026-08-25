using System;
using System.Collections.Generic;
using TowerDefense3D.Core;
using TowerDefense3D.Enemies;
using TowerDefense3D.Towers;

namespace TowerDefense3D.Waves
{
    public sealed class WaveSystem : IWaveSystem
    {
        private readonly WaveScheduleDefinition schedule;
        private readonly EnemySystem enemySystem;
        private readonly TowerNetworkSystem towerNetworkSystem;
        private readonly WaveSpawnPlanner spawnPlanner;
        private readonly StateMachine<WavePhase> stateMachine =
            new StateMachine<WavePhase>(WavePhase.Preparation, CanTransition);
        private IReadOnlyList<WaveSpawnOrder> currentPlan = Array.Empty<WaveSpawnOrder>();
        private int nextWaveIndex;
        private int nextSpawnIndex;
        private float elapsedSeconds;

        public WaveSystem(
            WaveScheduleDefinition schedule,
            EnemySystem enemySystem,
            TowerNetworkSystem towerNetworkSystem,
            WaveSpawnPlanner spawnPlanner)
        {
            this.schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
            this.enemySystem = enemySystem ?? throw new ArgumentNullException(nameof(enemySystem));
            this.towerNetworkSystem = towerNetworkSystem
                ?? throw new ArgumentNullException(nameof(towerNetworkSystem));
            this.spawnPlanner = spawnPlanner ?? throw new ArgumentNullException(nameof(spawnPlanner));

            IReadOnlyList<string> errors = schedule.CollectValidationErrors();
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join("\n", errors));
            }
        }

        public event Action StateChanged;

        public WavePhase Phase => stateMachine.CurrentState;
        public bool IsRunning => Phase == WavePhase.Running;
        public int WaveCount => schedule.Waves.Count;
        public int CurrentWaveNumber => Math.Min(nextWaveIndex + 1, WaveCount);

        public WaveState CreateState()
        {
            return new WaveState(
                Phase,
                CurrentWaveNumber,
                WaveCount,
                enemySystem.LivingCount,
                Phase == WavePhase.Preparation && towerNetworkSystem.HasValidChain);
        }

        public IReadOnlyList<EnemySpawnBatchDefinition> GetNextWavePreview()
        {
            if (Phase == WavePhase.Victory)
            {
                return Array.Empty<EnemySpawnBatchDefinition>();
            }

            return schedule.Waves[nextWaveIndex].SpawnBatches;
        }

        public bool TryStartWave(out string error)
        {
            if (Phase == WavePhase.Running)
            {
                error = "A wave is already running.";
                return false;
            }

            if (Phase == WavePhase.Victory)
            {
                error = "Every wave is complete.";
                return false;
            }

            if (!towerNetworkSystem.TryStartSimulation(out error))
            {
                return false;
            }

            currentPlan = spawnPlanner.CreatePlan(schedule, nextWaveIndex);
            nextSpawnIndex = 0;
            elapsedSeconds = 0f;
            stateMachine.TransitionTo(WavePhase.Running);
            SpawnDueEnemies();
            StateChanged?.Invoke();
            return true;
        }

        public void StepSpawning(float stepSeconds)
        {
            if (!IsRunning)
            {
                return;
            }

            elapsedSeconds += stepSeconds;
            SpawnDueEnemies();
        }

        public void CompleteStep()
        {
            if (!IsRunning
                || nextSpawnIndex < currentPlan.Count
                || enemySystem.LivingCount > 0)
            {
                return;
            }

            towerNetworkSystem.StopSimulation();
            nextWaveIndex++;
            stateMachine.TransitionTo(
                nextWaveIndex >= schedule.Waves.Count
                    ? WavePhase.Victory
                    : WavePhase.Preparation);
            StateChanged?.Invoke();
        }

        public void Reset()
        {
            towerNetworkSystem.StopSimulation();
            enemySystem.Reset();
            currentPlan = Array.Empty<WaveSpawnOrder>();
            nextWaveIndex = 0;
            nextSpawnIndex = 0;
            elapsedSeconds = 0f;
            stateMachine.TransitionTo(WavePhase.Preparation);
            StateChanged?.Invoke();
        }

        private void SpawnDueEnemies()
        {
            while (nextSpawnIndex < currentPlan.Count
                && currentPlan[nextSpawnIndex].TimeSeconds <= elapsedSeconds)
            {
                enemySystem.Spawn(currentPlan[nextSpawnIndex].Enemy);
                nextSpawnIndex++;
            }
        }

        private static bool CanTransition(WavePhase currentPhase, WavePhase nextPhase)
        {
            switch (currentPhase)
            {
                case WavePhase.Preparation:
                    return nextPhase == WavePhase.Running;
                case WavePhase.Running:
                    return nextPhase == WavePhase.Preparation
                        || nextPhase == WavePhase.Victory;
                case WavePhase.Victory:
                    return nextPhase == WavePhase.Preparation;
                default:
                    return false;
            }
        }
    }
}
