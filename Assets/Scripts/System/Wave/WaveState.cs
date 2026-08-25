namespace TowerDefense3D.Waves
{
    public enum WavePhase
    {
        Preparation,
        Running,
        Victory
    }

    public readonly struct WaveState
    {
        public WaveState(
            WavePhase phase,
            int currentWaveNumber,
            int waveCount,
            int livingEnemyCount)
        {
            Phase = phase;
            CurrentWaveNumber = currentWaveNumber;
            WaveCount = waveCount;
            LivingEnemyCount = livingEnemyCount;
        }

        public WavePhase Phase { get; }
        public int CurrentWaveNumber { get; }
        public int WaveCount { get; }
        public int LivingEnemyCount { get; }
    }
}
