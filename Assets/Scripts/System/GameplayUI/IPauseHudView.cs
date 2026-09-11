using System;

namespace TowerDefense3D.GameFlow
{
    public interface IPauseHudView
    {
        event Action PauseToggleRequested;

        /// <summary>Raised when the player taps the double-speed button.</summary>
        event Action FastForwardToggleRequested;

        void Initialize();
        void Render(bool isPaused);

        /// <summary>
        /// Shows whether the simulation is running at double speed.
        /// </summary>
        void RenderFastForward(bool isFastForward);
        void Show();
        void Hide();
        void Shutdown();
    }
}
