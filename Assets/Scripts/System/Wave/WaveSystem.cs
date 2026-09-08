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

            IReadOnlyList<EnemySpawnBatchDefinition> batches =
                schedule.Waves[nextWaveIndex].SpawnBatches;
            StationaryBossPlan boss = schedule.StationaryBoss;
            if (boss == null || !boss.IsFightingOnWave(nextWaveIndex + 1, schedule.Waves.Count))
            {
                return batches;
            }

            // The wave the boss fights on. It is not authored as a spawn batch - the boss is
            // already standing on the road - so the preview would otherwise announce an empty
            // wave right before the hardest one.
            var preview = new List<EnemySpawnBatchDefinition>(batches.Count + 1);
            preview.AddRange(batches);
            preview.Add(new EnemySpawnBatchDefinition(boss.Boss, 1));
            return preview;
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

            RefreshStandingBoss();
            currentPlan = AssignEnemyIds(
                spawnPlanner.CreatePlan(schedule, nextWaveIndex, ResolveStandDistance()));
            nextSpawnIndex = 0;
            elapsedSeconds = 0f;
            HandStandingBossToPlan();
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

        /// <summary>
        /// Development cheat: counts the current wave as beaten and moves on to the next, or to
        /// victory when it was the last one.
        /// </summary>
        /// <remarks>
        /// The wave's clear reward is paid, so what follows is the state a genuine clear would
        /// have left - the next wave's preparation, with the gold that wave earned. Skipping
        /// without it would make every later wave poorer than in a real run, which is the opposite
        /// of useful for testing.
        ///
        /// Phases are walked rather than jumped, the same way <see cref="ForceVictory"/> walks
        /// them: the state machine only accepts certain moves, and going through the real ones
        /// leaves the simulation shut down exactly as a real clear does.
        /// </remarks>
        public void ForceSkipWave()
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

            if (Phase == WavePhase.Defeat)
            {
                stateMachine.TransitionTo(WavePhase.Preparation);
            }

            goldSystem.Add(schedule.Waves[nextWaveIndex].ClearGoldReward);
            nextWaveIndex++;

            if (Phase == WavePhase.Preparation)
            {
                stateMachine.TransitionTo(WavePhase.Running);
            }

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
                // Either the boss has not walked on yet - nothing to place - or this is the last
                // wave, where it is left standing for now and the plan takes the very same
                // instance over once it has issued the id it will drive it by.
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
                nextSpawnIndex++;

                // A takeover order has nothing to spawn: the enemy is already on the board under
                // this id, and the planned frames are about to start moving it.
                if (order.AdoptsExistingEnemy && enemySystem.IsSpawned(order.EnemyId))
                {
                    continue;
                }

                enemySystem.Spawn(
                    order.EnemyId,
                    order.Enemy,
                    order.SpawnPointIndex,
                    order.StartDistanceMeters,
                    isStanding: false,
                    suppressEntranceEffect: order.SuppressEntranceEffect);
            }
        }

        /// <summary>
        /// Gives every order its enemy id, takeover orders included.
        /// </summary>
        /// <remarks>
        /// Every order reserves, with no exceptions. The planner continues its own summon ids from
        /// the highest id in the plan, and the live side continues from its counter; those two
        /// only stay in step while the plan's highest id is the last one reserved. An order that
        /// skipped reserving broke exactly that, and the summons it planned were then looked up
        /// under ids nothing on the board answered to.
        /// </remarks>
        private IReadOnlyList<WaveSpawnOrder> AssignEnemyIds(IReadOnlyList<WaveSpawnOrder> plan)
        {
            var assignedPlan = new WaveSpawnOrder[plan.Count];
            for (int index = 0; index < plan.Count; index++)
            {
                assignedPlan[index] = plan[index].WithEnemyId(enemySystem.ReserveEnemyId());
            }

            return assignedPlan;
        }

        /// <summary>
        /// Moves the standing boss onto the id the plan issued for it, so the instance the player
        /// has been watching is the one the plan drives.
        /// </summary>
        private void HandStandingBossToPlan()
        {
            if (!enemySystem.HasStandingBoss)
            {
                return;
            }

            for (int index = 0; index < currentPlan.Count; index++)
            {
                WaveSpawnOrder order = currentPlan[index];
                if (order.AdoptsExistingEnemy)
                {
                    enemySystem.AdoptStandingBossAs(order.EnemyId);
                    return;
                }
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
