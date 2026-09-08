using System;
using System.Collections.Generic;

namespace TowerDefense3D.Waves
{
    public interface IWaveSystem
    {
        event Action StateChanged;

        bool IsRunning { get; }

        WaveState CreateState();
        IReadOnlyList<EnemySpawnBatchDefinition> GetNextWavePreview();
        bool TryStartWave(out string error);

        /// <summary>
        /// Development cheat: clears the board and declares every remaining wave beaten, so the
        /// victory flow can be reached without playing the level out.
        /// </summary>
        void ForceVictory();

        /// <summary>
        /// Development cheat: counts the current wave as beaten and moves on, paying its clear
        /// reward, so a single wave can be stepped past without playing it.
        /// </summary>
        void ForceSkipWave();
    }
}
