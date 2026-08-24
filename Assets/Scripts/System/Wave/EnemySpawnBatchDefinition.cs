using System;
using System.Collections.Generic;
using TowerDefense3D.Enemies;
using UnityEngine;

namespace TowerDefense3D.Waves
{
    [Serializable]
    public sealed class EnemySpawnBatchDefinition
    {
        [SerializeField] private EnemyDefinition enemy;
        [SerializeField, Min(1)] private int count = 1;
        [SerializeField, Min(0f)] private float startTimeSeconds;
        [SerializeField, Min(0f)] private float spawnWindowSeconds;

        public EnemyDefinition Enemy => enemy;
        public int Count => count;
        public float StartTimeSeconds => startTimeSeconds;
        public float SpawnWindowSeconds => spawnWindowSeconds;

        internal void CollectValidationErrors(ICollection<string> errors, string context)
        {
            if (enemy == null)
            {
                errors.Add($"{context}: Enemy is required.");
            }

            if (count <= 0)
            {
                errors.Add($"{context}: Count must be greater than zero.");
            }

            if (startTimeSeconds < 0f)
            {
                errors.Add($"{context}: Start Time Seconds cannot be negative.");
            }

            if (spawnWindowSeconds < 0f)
            {
                errors.Add($"{context}: Spawn Window Seconds cannot be negative.");
            }
        }
    }
}
