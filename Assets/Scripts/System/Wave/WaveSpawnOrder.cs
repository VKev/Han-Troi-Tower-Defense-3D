using System;
using TowerDefense3D.Enemies;
using UnityEngine;

namespace TowerDefense3D.Waves
{
    public readonly struct WaveSpawnOrder
    {
        public WaveSpawnOrder(float timeSeconds, EnemyDefinition enemy, int sequence)
            : this(timeSeconds, enemy, sequence, -1, 0L, 0f, false, false)
        {
        }

        public WaveSpawnOrder(
            float timeSeconds,
            EnemyDefinition enemy,
            int sequence,
            int spawnPointIndex)
            : this(timeSeconds, enemy, sequence, spawnPointIndex, 0L, 0f, false, false)
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
            float startDistanceMeters,
            bool suppressEntranceEffect = false,
            bool adoptsExistingEnemy = false)
            : this(
                timeSeconds,
                enemy,
                sequence,
                spawnPointIndex,
                0L,
                startDistanceMeters,
                suppressEntranceEffect,
                adoptsExistingEnemy)
        {
        }

        private WaveSpawnOrder(
            float timeSeconds,
            EnemyDefinition enemy,
            int sequence,
            int spawnPointIndex,
            long enemyId,
            float startDistanceMeters,
            bool suppressEntranceEffect,
            bool adoptsExistingEnemy)
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
            SuppressEntranceEffect = suppressEntranceEffect;
            AdoptsExistingEnemy = adoptsExistingEnemy;
        }

        public float TimeSeconds { get; }
        public EnemyDefinition Enemy { get; }
        public int SpawnPointIndex { get; }

        /// <summary>How far along its route the enemy begins. Zero for an ordinary spawn.</summary>
        public float StartDistanceMeters { get; }

        /// <summary>
        /// Arrives with no entrance effect, for an enemy that is continuing rather than appearing.
        /// </summary>
        public bool SuppressEntranceEffect { get; }

        /// <summary>
        /// Takes over an enemy that is already standing on the board instead of spawning one.
        /// </summary>
        /// <remarks>
        /// The plan drives enemies by id, so handing this order the id the standing boss already
        /// carries makes the planned frames move that very instance. Nothing is created and
        /// nothing is destroyed - the boss that has been standing there simply starts walking.
        /// </remarks>
        public bool AdoptsExistingEnemy { get; }
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
                StartDistanceMeters,
                SuppressEntranceEffect,
                AdoptsExistingEnemy);
        }
    }
}
