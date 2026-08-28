using System;

namespace TowerDefense3D.GameFlow
{
    public interface IPauseHudView
    {
        event Action PauseToggleRequested;

        void Initialize();
        void Render(bool isPaused);
        void Show();
        void Shutdown();
    }
}
