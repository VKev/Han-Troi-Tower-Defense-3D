using System;

namespace TowerDefense3D.GameFlow
{
    public interface ILevelVictoryEscapeView
    {
        event Action EscapeCompleted;

        void PlayEscape();
    }
}
