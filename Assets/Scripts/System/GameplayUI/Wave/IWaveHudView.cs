using System;
using TowerDefense3D.Enemies;

namespace TowerDefense3D.GameFlow
{
    public interface IWaveHudView
    {
        event Action StartWaveRequested;
        event Action<EnemyDefinition> EnemyDescriptionOpened;

        void Initialize();
        void Render(WaveHudState state);
        void Show();
    }
}
