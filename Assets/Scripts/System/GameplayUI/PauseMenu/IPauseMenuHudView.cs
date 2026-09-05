using System;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// The modal that fronts a paused level: carry on, start over, or leave.
    /// </summary>
    /// <remarks>
    /// Deliberately three commands and no more. Skipping to the next level lives on the outcome
    /// panel, where it is earned; offering it from a pause would let a player leave a level they
    /// are still in the middle of.
    /// </remarks>
    public interface IPauseMenuHudView
    {
        event Action ResumeRequested;
        event Action RestartRequested;
        event Action ReturnToLevelMenuRequested;

        void Initialize();
        void Render(PauseMenuHudState state);
        void Shutdown();
    }
}
