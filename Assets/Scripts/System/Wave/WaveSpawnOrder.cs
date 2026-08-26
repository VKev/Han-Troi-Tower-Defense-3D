using System;
using TowerDefense3D.Enemies;

namespace TowerDefense3D.Waves
{
    public readonly struct WaveSpawnOrder
    {
        public WaveSpawnOrder(float timeSeconds, EnemyDefinition enemy, int sequence)
        {
            if (timeSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(timeSeconds));
            }

            TimeSeconds = timeSeconds;
            Enemy = enemy ?? throw new ArgumentNullException(nameof(enemy));
            Sequence = sequence;
        }

        public float TimeSeconds { get; }
        public EnemyDefinition Enemy { get; }
        internal int Sequence { get; }
    }
}
