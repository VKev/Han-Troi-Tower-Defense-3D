using System;
using System.Collections.Generic;
using TowerDefense3D.Core;
using TowerDefense3D.Economy;
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
        private readonly LevelGoldSystem goldSystem;
        private readonly LevelBaseHealthSystem healthSystem;
        private readonly IStandingBossAnchor standingBossAnchor;
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
            WaveSpawnPlanner spawnPlanner,
            LevelGoldSystem goldSystem,
            LevelBaseHealthSystem healthSystem,
            IStandingBossAnchor standingBossAnchor = null)
        {
            this.standingBossAnchor = standingBossAnchor;
            this.schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
            this.enemySystem = enemySystem ?? throw new ArgumentNullException(nameof(enemySystem));
            this.towerNetworkSystem = towerNetworkSystem
                ?? throw new ArgumentNullException(nameof(towerNetworkSystem));
            this.spawnPlanner = spawnPlanner ?? throw new ArgumentNullException(nameof(spawnPlanner));
            this.goldSystem = goldSystem ?? throw new ArgumentNullException(nameof(goldSystem));
            this.healthSystem = healthSystem ?? throw new ArgumentNullException(nameof(healthSystem));

            IReadOnlyList<string> errors = schedule.CollectValidationErrors();
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join("\n", errors));
            }
        }

        public event Action StateChanged;
        public event Action<IReadOnlyList<WaveSpawnOrder>> WavePlanCreated;

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
                Phase == WavePhase.Preparation && towerNetworkSystem.HasValidChain,
                NextWaveClearGold,
                RemainingEnemyCount);
        }

        /// <summary>
        /// How many enemies are left to deal with in the wave the HUD is showing.
        /// </summary>
        /// <remarks>
        /// Before the wave starts this is the wave's whole roster, so the plaque advertises the
        /// size of what is coming instead of a living count that is necessarily zero.
        ///
        /// While it runs the number is the unspawned remainder plus what is on the board. A spawn
        /// therefore moves nothing - one leaves the queue exactly as one arrives - and only a
        /// death or a leak brings it down. A summoner is the one thing that pushes it back up,
        /// because its brood enters through the combat timeline rather than the wave plan and so
        /// was never in the queue this counts.
        /// </remarks>
        private int RemainingEnemyCount
        {
            get
            {
                if (Phase == WavePhase.Running)
                {
                    return currentPlan.Count - nextSpawnIndex + enemySystem.LivingCount;
                }

                // Past the last wave there is no roster left to read, and nextWaveIndex has
                // walked off the end of the schedule.
                if (Phase == WavePhase.Victory)
                {
                    return 0;
                }

                IReadOnlyList<EnemySpawnBatchDefinition> batches =
                    schedule.Waves[nextWaveIndex].SpawnBatches;
                int roster = 0;
                for (int index = 0; index < batches.Count; index++)
                {
                    roster += batches[index].Count;
                }

                return roster;
            }
        }

        private int NextWaveClearGold => Phase == WavePhase.Victory
            ? 0
            : schedule.Waves[nextWaveIndex].ClearGoldReward;

        public IReadOnlyList<EnemySpawnBatchDefinition> GetNextWavePreview()
        {
            if (Phase == WavePhase.Victory)
            {
                return Array.Empty<EnemySpawnBatchDefinition>();
            }

            if (Phase == WavePhase.Defeat)
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

            if (Phase == WavePhase.Defeat)
            {
                error = "The Toad has no HP remaining.";
                return false;
            }

            if (!towerNetworkSystem.TryStartSimulation(out error))
            {
                return false;
            }

            currentPlan = AssignEnemyIds(
                spawnPlanner.CreatePlan(schedule, nextWaveIndex, ResolveStandDistance()));
            nextSpawnIndex = 0;
            elapsedSeconds = 0f;
            RefreshStandingBoss();
            try
            {
                WavePlanCreated?.Invoke(currentPlan);
            }
            catch (InvalidOperationException planningFailure)
            {
                // Planning runs before the wave starts, so a failure must leave the board
                // exactly as it was rather than stranding it in a started simulation.
                towerNetworkSystem.StopSimulation();
                currentPlan = Array.Empty<WaveSpawnOrder>();
                error = planningFailure.Message;
                StateChanged?.Invoke();
                return false;
            }

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
            enemySystem.StepStandingBossCasts(elapsedSeconds, stepSeconds);
            SpawnDueEnemies();
        }

        public void CompleteStep()
        {
            if (!IsRunning)
            {
                return;
            }

            if (healthSystem.IsDepleted)
            {
                towerNetworkSystem.StopSimulation();
                stateMachine.TransitionTo(WavePhase.Defeat);
                StateChanged?.Invoke();
                return;
            }

            if (nextSpawnIndex < currentPlan.Count || enemySystem.LivingCount > 0)
            {
                return;
            }

            towerNetworkSystem.StopSimulation();
            goldSystem.Add(schedule.Waves[nextWaveIndex].ClearGoldReward);
            nextWaveIndex++;
            stateMachine.TransitionTo(
                nextWaveIndex >= schedule.Waves.Count
                    ? WavePhase.Victory
                    : WavePhase.Preparation);
            StateChanged?.Invoke();
        }

        /// <summary>
        /// Development cheat: wipes the board and reports every wave beaten. Phases are walked
        /// rather than jumped because the state machine only accepts Victory out of Running, and
        /// routing through the real transitions keeps the simulation shut down the same way a
        /// genuine clear does.
        /// </summary>
        public void ForceVictory()
        {
            if (Phase == WavePhase.Victory)
            {
                return;
            }

            towerNetworkSystem.StopSimulation();
            enemySystem.Reset();
            currentPlan = Array.Empty<WaveSpawnOrder>();
            nextSpawnIndex = 0;
            elapsedSeconds = 0f;
            nextWaveIndex = schedule.Waves.Count;

            if (Phase == WavePhase.Defeat)
            {
                stateMachine.TransitionTo(WavePhase.Preparation);
            }

            if (Phase == WavePhase.Preparation)
            {
                stateMachine.TransitionTo(WavePhase.Running);
            }

            stateMachine.TransitionTo(WavePhase.Victory);
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

        /// <summary>
        /// Puts the level's standing boss in place for this wave, or takes it away for the wave it
        /// joins the fight on.
        /// </summary>
        /// <remarks>
        /// Asked of the schedule rather than answered here, so this and the combat plan cannot
        /// come to different conclusions about which wave the boss fights on.
        /// </remarks>
        /// <summary>
        /// Where the boss waits: the scene marker if the level places one, otherwise the number on
        /// the schedule.
        /// </summary>
        /// <remarks>
        /// Measured each time a wave starts rather than stored, so moving the marker is the only
        /// step - there is nothing to press afterwards and nothing that can be left out of date.
        /// </remarks>
        private float ResolveStandDistance()
        {
            StationaryBossPlan plan = schedule.StationaryBoss;
            if (plan == null || !plan.IsAuthored)
            {
                return -1f;
            }

            if (standingBossAnchor == null || !standingBossAnchor.HasAnchor)
            {
                return plan.StandDistanceMeters;
            }

            return enemySystem.MeasureRoadDistance(
                plan.SpawnPointIndex,
                standingBossAnchor.WorldPosition);
        }

        private void RefreshStandingBoss()
        {
            StationaryBossPlan plan = schedule.StationaryBoss;
            if (plan == null || !plan.IsAuthored)
            {
                return;
            }

            int waveNumber = nextWaveIndex + 1;
            if (!plan.IsStandingOnWave(waveNumber, schedule.Waves.Count))
            {
                // The last wave: the plan spawns the boss as a real enemy at the same spot, so the
                // fixture has to go or there would be two of them standing on each other.
                enemySystem.RemoveStandingBoss();
                return;
            }

            IReadOnlyList<StationaryBossCast> casts = plan.GetCasts(waveNumber);
            var castTimes = new List<float>(casts.Count);
            for (int index = 0; index < casts.Count; index++)
            {
                if (casts[index] != null)
                {
                    castTimes.Add(casts[index].CastTimeSeconds);
                }
            }

            enemySystem.EnsureStandingBoss(
                plan.Boss,
                plan.SpawnPointIndex,
                ResolveStandDistance(),
                standingBossAnchor != null && standingBossAnchor.HasAnchor
                    ? standingBossAnchor.FacingYawDegrees
                    : 0f,
                castTimes,
                plan.Boss.SummonSkillDurationSeconds);
        }

        private void SpawnDueEnemies()
        {
            while (nextSpawnIndex < currentPlan.Count
                && currentPlan[nextSpawnIndex].TimeSeconds <= elapsedSeconds)
            {
                WaveSpawnOrder order = currentPlan[nextSpawnIndex];
                enemySystem.Spawn(
                    order.EnemyId,
                    order.Enemy,
                    order.SpawnPointIndex,
                    order.StartDistanceMeters,
                    isStanding: false);
                nextSpawnIndex++;
            }
        }

        private IReadOnlyList<WaveSpawnOrder> AssignEnemyIds(
            IReadOnlyList<WaveSpawnOrder> plan)
        {
            var assignedPlan = new WaveSpawnOrder[plan.Count];
            for (int index = 0; index < plan.Count; index++)
            {
                assignedPlan[index] = plan[index].WithEnemyId(enemySystem.ReserveEnemyId());
            }

            return assignedPlan;
        }

        private static bool CanTransition(WavePhase currentPhase, WavePhase nextPhase)
        {
            switch (currentPhase)
            {
                case WavePhase.Preparation:
                    return nextPhase == WavePhase.Running;
                case WavePhase.Running:
                    return nextPhase == WavePhase.Preparation
                        || nextPhase == WavePhase.Victory
                        || nextPhase == WavePhase.Defeat;
                case WavePhase.Victory:
                    return nextPhase == WavePhase.Preparation;
                case WavePhase.Defeat:
                    return nextPhase == WavePhase.Preparation;
                default:
                    return false;
            }
        }
    }
}
