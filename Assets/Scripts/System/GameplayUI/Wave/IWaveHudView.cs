using System;

namespace TowerDefense3D.GameFlow
{
    public interface IWaveHudView
    {
        event Action StartWaveRequested;

        void Initialize();
        void Render(WaveHudState state);
        void Show();
    }
}
