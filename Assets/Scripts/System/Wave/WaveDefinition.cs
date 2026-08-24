using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.Waves
{
    [Serializable]
    public sealed class WaveDefinition
    {
        [SerializeField] private List<EnemySpawnBatchDefinition> spawnBatches = new List<EnemySpawnBatchDefinition>();

        public IReadOnlyList<EnemySpawnBatchDefinition> SpawnBatches => spawnBatches;

        internal void CollectValidationErrors(ICollection<string> errors, int waveIndex)
        {
            string waveContext = $"Wave {waveIndex + 1}";
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
