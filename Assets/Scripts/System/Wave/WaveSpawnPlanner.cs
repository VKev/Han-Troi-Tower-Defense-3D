using System;
using System.Collections.Generic;

namespace TowerDefense3D.Waves
{
    public sealed class WaveSpawnPlanner
    {
        public IReadOnlyList<WaveSpawnOrder> CreatePlan(
            WaveScheduleDefinition schedule,
            int waveIndex)
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

            orders.Sort(CompareOrders);
            return orders;
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
