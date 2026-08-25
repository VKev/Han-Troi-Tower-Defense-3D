namespace TowerDefense3D.GameFlow
{
    public readonly struct WaveHudState
    {
        public WaveHudState(
            string waveText,
            string previewText,
            bool startWaveEnabled)
        {
            WaveText = waveText;
            PreviewText = previewText;
            StartWaveEnabled = startWaveEnabled;
        }

        public string WaveText { get; }
        public string PreviewText { get; }
        public bool StartWaveEnabled { get; }
    }
}
