using System.Collections.Generic;
using UnityEngine;

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
            IReadOnlyList<Sprite> previewIcons,
            bool startWaveEnabled)
        {
            WaveCounterText = waveCounterText;
            StatusText = statusText;
            WaveProgress = waveProgress;
            EnemiesLeftText = enemiesLeftText;
            StartWaveText = startWaveText;
            StartWaveBonusText = startWaveBonusText;
            PreviewIcons = previewIcons;
            StartWaveEnabled = startWaveEnabled;
        }

        public string WaveCounterText { get; }
        public string StatusText { get; }
        public float WaveProgress { get; }
        public string EnemiesLeftText { get; }
        public string StartWaveText { get; }
        public string StartWaveBonusText { get; }

        /// <summary>
        /// One portrait per distinct enemy in the wave on show, in the order the schedule sends
        /// them. Enemies with no portrait assigned are left out rather than drawn as a hole.
        /// </summary>
        /// <remarks>
        /// Filled in every phase. The grid is the player's to open and shut, so it has to have
        /// something to show whenever they choose to open it.
        /// </remarks>
        public IReadOnlyList<Sprite> PreviewIcons { get; }

        public bool StartWaveEnabled { get; }
    }
}
