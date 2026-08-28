using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.Waves
{
    [Serializable]
    public sealed class WaveDefinition
    {
        [SerializeField, Min(0)] private int clearGoldReward = 100;
        [SerializeField] private List<EnemySpawnBatchDefinition> spawnBatches = new List<EnemySpawnBatchDefinition>();

        public int ClearGoldReward => clearGoldReward;
        public IReadOnlyList<EnemySpawnBatchDefinition> SpawnBatches => spawnBatches;

        internal void CollectValidationErrors(ICollection<string> errors, int waveIndex)
        {
            string waveContext = $"Wave {waveIndex + 1}";
            if (clearGoldReward < 0)
            {
                errors.Add($"{waveContext}: Clear Gold Reward cannot be negative.");
            }

            if (spawnBatches.Count == 0)
            {
                errors.Add($"{waveContext}: At least one Spawn Batch is required.");
                return;
            }

            for (int batchIndex = 0; batchIndex < spawnBatches.Count; batchIndex++)
            {
                spawnBatches[batchIndex].CollectValidationErrors(errors, $"{waveContext}, batch {batchIndex + 1}");
            }
        }
    }
}
