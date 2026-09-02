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
    }
}
