using System;
using TowerDefense3D.Enemies;

namespace TowerDefense3D.Waves
{
    public readonly struct WaveSpawnOrder
    {
        public WaveSpawnOrder(float timeSeconds, EnemyDefinition enemy, int sequence)
            : this(timeSeconds, enemy, sequence, 0L)
        {
        }

        private WaveSpawnOrder(
            float timeSeconds,
            EnemyDefinition enemy,
            int sequence,
            long enemyId)
        {
            if (timeSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(timeSeconds));
            }

            TimeSeconds = timeSeconds;
            Enemy = enemy ?? throw new ArgumentNullException(nameof(enemy));
            Sequence = sequence;
            EnemyId = enemyId;
        }

        public float TimeSeconds { get; }
        public EnemyDefinition Enemy { get; }
        internal int Sequence { get; }
        internal long EnemyId { get; }

        internal WaveSpawnOrder WithEnemyId(long enemyId)
        {
            return new WaveSpawnOrder(TimeSeconds, Enemy, Sequence, enemyId);
        }
    }
}
