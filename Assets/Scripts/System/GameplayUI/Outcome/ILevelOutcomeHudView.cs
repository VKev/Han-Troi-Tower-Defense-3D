using System;

namespace TowerDefense3D.GameFlow
{
    public interface ILevelOutcomeHudView
    {
        event Action PlayAgainRequested;
        event Action NextLevelRequested;
        event Action ReturnToLevelMenuRequested;

        void Initialize();
        void Render(LevelOutcomeHudState state);
        void Shutdown();
    }
}
