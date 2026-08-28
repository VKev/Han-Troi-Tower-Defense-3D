namespace TowerDefense3D.GameFlow
{
    public readonly struct WaveHudState
    {
        public WaveHudState(
            string waveCounterText,
            string statusText,
            float waveProgress,
            string enemiesLeftText,
            string startWaveText,
            string startWaveBonusText,
            string previewText,
            bool startWaveEnabled)
        {
            WaveCounterText = waveCounterText;
            StatusText = statusText;
            WaveProgress = waveProgress;
            EnemiesLeftText = enemiesLeftText;
            StartWaveText = startWaveText;
            StartWaveBonusText = startWaveBonusText;
            PreviewText = previewText;
            StartWaveEnabled = startWaveEnabled;
        }

        public string WaveCounterText { get; }
        public string StatusText { get; }
        public float WaveProgress { get; }
        public string EnemiesLeftText { get; }
        public string StartWaveText { get; }
        public string StartWaveBonusText { get; }
        public string PreviewText { get; }
        public bool StartWaveEnabled { get; }
    }
}
