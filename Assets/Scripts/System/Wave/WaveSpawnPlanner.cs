using System;
using System.Collections.Generic;
using TowerDefense3D.Enemies;

namespace TowerDefense3D.Waves
{
    public sealed class WaveSpawnPlanner
    {
        public IReadOnlyList<WaveSpawnOrder> CreatePlan(
            WaveScheduleDefinition schedule,
            int waveIndex)
        {
            return CreatePlan(schedule, waveIndex, standDistanceMeters: -1f);
        }

        /// <summary>
        /// <paramref name="standDistanceMeters"/> is where the standing boss waits, measured from
        /// the scene marker when the level has one. A negative value falls back to the schedule.
        /// </summary>
        public IReadOnlyList<WaveSpawnOrder> CreatePlan(
            WaveScheduleDefinition schedule,
            int waveIndex,
            float standDistanceMeters)
        {
            if (schedule == null)
            {
                throw new ArgumentNullException(nameof(schedule));
            }

            if (waveIndex < 0 || waveIndex >= schedule.Waves.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(waveIndex));
            }

            var random = new Random(CombineSeed(schedule.RandomSeed, waveIndex));
            var orders = new List<WaveSpawnOrder>();
            IReadOnlyList<EnemySpawnBatchDefinition> batches =
                schedule.Waves[waveIndex].SpawnBatches;
            int sequence = 0;

            for (int batchIndex = 0; batchIndex < batches.Count; batchIndex++)
            {
                EnemySpawnBatchDefinition batch = batches[batchIndex];
                for (int countIndex = 0; countIndex < batch.Count; countIndex++)
                {
                    float offset = batch.SpawnWindowSeconds <= 0f
                        ? 0f
                        : (float)random.NextDouble() * batch.SpawnWindowSeconds;
                    orders.Add(new WaveSpawnOrder(
                        batch.StartTimeSeconds + offset,
                        batch.Enemy,
                        sequence++,
                        batch.SpawnPointIndex));
                }
            }

            AppendStandingBoss(schedule, waveIndex, standDistanceMeters, orders, ref sequence);
            orders.Sort(CompareOrders);
            return orders;
        }

        /// <summary>
        /// Adds the boss that stands on the road this wave, and the enemies its casts produce.
        /// </summary>
        /// <remarks>
        /// The summons are written out here as ordinary spawn orders rather than left for the
        /// combat planner to conjure at run time. Everything the planner needs then arrives the
        /// way every other enemy arrives, so there is one spawning path instead of two that have
        /// to agree.
        ///
        /// Their placement comes from <see cref="BossSummonSchedule"/>, the same code the fighting
        /// boss uses, so a summon steps out beside the boss with the same spacing whether the boss
        /// is standing or moving.
        ///
        /// The standing boss itself is deliberately absent. It cannot be hurt and never moves, so
        /// it would sit in the planner's enemy list for ever and the plan - which finishes when
        /// that list empties - would run out its tick horizon and be rejected. It has no combat
        /// role until the last wave, so it has no business in the deterministic plan; it is a live
        /// fixture instead. Only what it produces has to be planned.
        /// </remarks>
        private static void AppendStandingBoss(
            WaveScheduleDefinition schedule,
            int waveIndex,
            float standDistanceMeters,
            List<WaveSpawnOrder> orders,
            ref int sequence)
        {
            StationaryBossPlan plan = schedule.StationaryBoss;
            if (plan == null || !plan.IsAuthored)
            {
                return;
            }

            float standDistance = standDistanceMeters >= 0f
                ? standDistanceMeters
                : plan.StandDistanceMeters;

            int waveNumber = waveIndex + 1;

            // The waves before the boss walks on are ordinary waves. Checked in its own right
            // because "not standing" is true here as well, and letting the two share a branch is
            // how a fighting boss would be added to every early wave.
            if (!plan.IsPresentOnWave(waveNumber, schedule.Waves.Count))
            {
                return;
            }

            if (plan.IsFightingOnWave(waveNumber, schedule.Waves.Count))
            {
                // The last wave. The boss sets off from the spot it has been standing on, as an
                // ordinary enemy: it moves, it can be hurt, and the wave is not over until it
                // falls. Emitted here rather than left to a spawn batch because a batch cannot say
                // "start part way along the road", and starting it back at the road mouth would
                // teleport it away from where the player has watched it stand.
                //
                // The same boss, not a replacement: this order takes over the instance that has
                // been standing here, so it walks off from where the player watched it wait.
                // Nothing spawns, so there is no entrance to suppress - though the flag is still
                // set for the case where no fixture exists to take over, such as replaying
                // straight into the last wave.
                orders.Add(new WaveSpawnOrder(
                    0f,
                    plan.Boss,
                    sequence++,
                    plan.SpawnPointIndex,
                    standDistance,
                    suppressEntranceEffect: true,
                    adoptsExistingEnemy: true));
                return;
            }

            IReadOnlyList<StationaryBossCast> casts = plan.GetCasts(waveNumber);
            SummonerBossEnemyDefinition boss = plan.Boss;
            var scheduled = new List<ScheduledSummon>();
            for (int castIndex = 0; castIndex < casts.Count; castIndex++)
            {
                StationaryBossCast cast = casts[castIndex];
                if (cast == null)
                {
                    continue;
                }

                BossSummonSchedule.BuildFromEntries(
                    boss,
                    cast.Summons,
                    bossId: waveNumber,
                    castVersion: castIndex,
                    scheduled);

                for (int index = 0; index < scheduled.Count; index++)
                {
                    ScheduledSummon summon = scheduled[index];
                    // Same road as the boss, or a summon steps out onto a different one.
                    orders.Add(new WaveSpawnOrder(
                        cast.CastTimeSeconds + summon.DueSeconds,
                        summon.Definition,
                        sequence++,
                        plan.SpawnPointIndex,
                        standDistance + summon.ForwardOffsetMeters));
                }
            }
        }

        private static int CombineSeed(int seed, int waveIndex)
        {
            unchecked
            {
                return (seed * 397) ^ waveIndex;
            }
        }

        private static int CompareOrders(WaveSpawnOrder left, WaveSpawnOrder right)
        {
            int timeComparison = left.TimeSeconds.CompareTo(right.TimeSeconds);
            return timeComparison != 0
                ? timeComparison
                : left.Sequence.CompareTo(right.Sequence);
        }
    }
}
