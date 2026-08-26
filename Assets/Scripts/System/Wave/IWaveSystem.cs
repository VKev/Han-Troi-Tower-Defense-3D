using System;
using System.Collections.Generic;

namespace TowerDefense3D.Waves
{
    public interface IWaveSystem
    {
        event Action StateChanged;

        WaveState CreateState();
        IReadOnlyList<EnemySpawnBatchDefinition> GetNextWavePreview();
        bool TryStartWave(out string error);
    }
}
