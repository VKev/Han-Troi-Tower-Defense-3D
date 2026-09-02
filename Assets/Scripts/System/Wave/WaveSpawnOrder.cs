using System;
using TowerDefense3D.Enemies;

namespace TowerDefense3D.Waves
{
    public readonly struct WaveSpawnOrder
    {
        public WaveSpawnOrder(float timeSeconds, EnemyDefinition enemy, int sequence)
            : this(timeSeconds, enemy, sequence, -1, 0L)
        {
        }

        public WaveSpawnOrder(
            float timeSeconds,
            EnemyDefinition enemy,
            int sequence,
            int spawnPointIndex)
            : this(timeSeconds, enemy, sequence, spawnPointIndex, 0L)
        {
        }

        private WaveSpawnOrder(
            float timeSeconds,
            EnemyDefinition enemy,
            int sequence,
            int spawnPointIndex,
            long enemyId)
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
        }

        public float TimeSeconds { get; }
        public EnemyDefinition Enemy { get; }
        public int SpawnPointIndex { get; }
        internal int Sequence { get; }
        internal long EnemyId { get; }

        internal WaveSpawnOrder WithEnemyId(long enemyId)
        {
            return new WaveSpawnOrder(TimeSeconds, Enemy, Sequence, SpawnPointIndex, enemyId);
        }
    }
}
