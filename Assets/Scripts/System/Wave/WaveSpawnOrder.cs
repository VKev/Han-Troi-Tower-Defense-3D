using System;
using TowerDefense3D.Enemies;
using UnityEngine;

namespace TowerDefense3D.Waves
{
    public readonly struct WaveSpawnOrder
    {
        public WaveSpawnOrder(float timeSeconds, EnemyDefinition enemy, int sequence)
            : this(timeSeconds, enemy, sequence, -1, 0L, 0f)
        {
        }

        public WaveSpawnOrder(
            float timeSeconds,
            EnemyDefinition enemy,
            int sequence,
            int spawnPointIndex)
            : this(timeSeconds, enemy, sequence, spawnPointIndex, 0L, 0f)
        {
        }

        /// <summary>
        /// An enemy that enters the road part way along it - the summons that step out beside the
        /// standing boss, and the boss itself on the wave it fights.
        /// </summary>
        public WaveSpawnOrder(
            float timeSeconds,
            EnemyDefinition enemy,
            int sequence,
            int spawnPointIndex,
            float startDistanceMeters)
            : this(timeSeconds, enemy, sequence, spawnPointIndex, 0L, startDistanceMeters)
        {
        }

        private WaveSpawnOrder(
            float timeSeconds,
            EnemyDefinition enemy,
            int sequence,
            int spawnPointIndex,
            long enemyId,
            float startDistanceMeters)
        {
            if (timeSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(timeSeconds));
            }

            TimeSeconds = timeSeconds;
            Enemy = enemy ?? throw new ArgumentNullException(nameof(enemy));
            Sequence = sequence;
            SpawnPointIndex = spawnPointIndex;
            EnemyId = enemyId;
            StartDistanceMeters = Mathf.Max(0f, startDistanceMeters);
        }

        public float TimeSeconds { get; }
        public EnemyDefinition Enemy { get; }
        public int SpawnPointIndex { get; }

        /// <summary>How far along its route the enemy begins. Zero for an ordinary spawn.</summary>
        public float StartDistanceMeters { get; }
        internal int Sequence { get; }
        internal long EnemyId { get; }

        internal WaveSpawnOrder WithEnemyId(long enemyId)
        {
            return new WaveSpawnOrder(
                TimeSeconds,
                Enemy,
                Sequence,
                SpawnPointIndex,
                enemyId,
                StartDistanceMeters);
        }
    }
}
