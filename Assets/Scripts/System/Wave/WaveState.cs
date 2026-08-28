namespace TowerDefense3D.Waves
{
    public enum WavePhase
    {
        Preparation,
        Running,
        Victory,
        Defeat
    }

    public readonly struct WaveState
    {
        public WaveState(
            WavePhase phase,
            int currentWaveNumber,
            int waveCount,
            int livingEnemyCount,
            bool canStartWave,
            int nextWaveClearGold = 0)
        {
            Phase = phase;
            CurrentWaveNumber = currentWaveNumber;
            WaveCount = waveCount;
            LivingEnemyCount = livingEnemyCount;
            CanStartWave = canStartWave;
            NextWaveClearGold = nextWaveClearGold;
        }

        public WavePhase Phase { get; }
        public int CurrentWaveNumber { get; }
        public int WaveCount { get; }
        public int LivingEnemyCount { get; }
        public bool CanStartWave { get; }
        public int NextWaveClearGold { get; }
    }
}
