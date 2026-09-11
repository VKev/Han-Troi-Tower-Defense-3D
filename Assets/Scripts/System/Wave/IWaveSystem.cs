using System;
using System.Collections.Generic;

namespace TowerDefense3D.Waves
{
    public interface IWaveSystem
    {
        event Action StateChanged;

        bool IsRunning { get; }
        bool IsCurrentWaveRetryAvailable { get; }

        /// <summary>
        /// Whether the wave now being prepared has already been attempted and retried, so nudges
        /// meant for a first attempt do not fire again on the replay.
        /// </summary>
        bool HasRetriedCurrentWave { get; }

        WaveState CreateState();
        IReadOnlyList<EnemySpawnBatchDefinition> GetNextWavePreview();
        bool TryStartWave(out string error);
        bool RetryCurrentWave();

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
