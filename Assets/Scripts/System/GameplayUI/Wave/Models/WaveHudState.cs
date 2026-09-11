using System.Collections.Generic;
using TowerDefense3D.Enemies;
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
            bool startWaveEnabled,
            IReadOnlyList<EnemyDefinition> previewEnemies = null,
            IReadOnlyList<bool> previewEnemiesAreNew = null,
            bool showStartWaveBlockedHint = false)
        {
            WaveCounterText = waveCounterText;
            StatusText = statusText;
            WaveProgress = waveProgress;
            EnemiesLeftText = enemiesLeftText;
            StartWaveText = startWaveText;
            StartWaveBonusText = startWaveBonusText;
            PreviewIcons = previewIcons;
            StartWaveEnabled = startWaveEnabled;
            PreviewEnemies = previewEnemies;
            PreviewEnemiesAreNew = previewEnemiesAreNew;
            ShowStartWaveBlockedHint = showStartWaveBlockedHint;
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
        public IReadOnlyList<EnemyDefinition> PreviewEnemies { get; }
        public IReadOnlyList<bool> PreviewEnemiesAreNew { get; }

        public bool StartWaveEnabled { get; }

        /// <summary>
        /// Whether to stand the "link a chain first" line above the build bar.
        /// </summary>
        /// <remarks>
        /// Narrower than the negation of <see cref="StartWaveEnabled"/>, which is also false
        /// while a wave runs and after the level is decided. Those are not states the player can
        /// fix by linking anything, so a line telling them to link would be wrong there.
        /// </remarks>
        public bool ShowStartWaveBlockedHint { get; }
    }
}
