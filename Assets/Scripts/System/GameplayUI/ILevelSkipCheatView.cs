using System;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// The development shortcut that declares the level won on the spot. It exists so the victory
    /// flow - the Cóc's escape and the outcome panel behind it - can be watched without playing
    /// every wave, and is deliberately its own view so deleting the cheat is deleting one button.
    /// </summary>
    public interface ILevelSkipCheatView
    {
        event Action SkipToVictoryRequested;

        /// <summary>One wave counted as beaten, rather than the whole level.</summary>
        event Action SkipWaveRequested;

        void Initialize();
        void Render(bool canSkip);
        void Show();
        void Shutdown();
    }
}
