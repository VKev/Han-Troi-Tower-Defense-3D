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
            int nextWaveClearGold = 0,
            int remainingEnemyCount = 0)
        {
            Phase = phase;
            CurrentWaveNumber = currentWaveNumber;
            WaveCount = waveCount;
            LivingEnemyCount = livingEnemyCount;
            CanStartWave = canStartWave;
            NextWaveClearGold = nextWaveClearGold;
            RemainingEnemyCount = remainingEnemyCount;
        }

        public WavePhase Phase { get; }
        public int CurrentWaveNumber { get; }
        public int WaveCount { get; }

        /// <summary>Enemies standing on the board right now.</summary>
        public int LivingEnemyCount { get; }

        /// <summary>
        /// Enemies the player still has to get through in the wave on show: the ones already out
        /// plus the ones the schedule has yet to send.
        /// </summary>
        /// <remarks>
        /// Distinct from <see cref="LivingEnemyCount"/> because a wave is a fixed roster, not a
        /// headcount that grows as it arrives. This one is the whole roster before the wave
        /// starts and falls only as enemies die, so a spawn moves nothing.
        /// </remarks>
        public int RemainingEnemyCount { get; }

        public bool CanStartWave { get; }
        public int NextWaveClearGold { get; }
    }
}
