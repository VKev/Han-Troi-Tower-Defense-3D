using System;
using System.Collections.Generic;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Renders application UI state and forwards authored UI callbacks.
    /// </summary>
    public interface IApplicationUIView
    {
        void Reset();
        void ShowLevelMenu(IReadOnlyList<LevelMenuItemState> levels, Action<int> onLevelSelected);
        void HideLevelMenu();
        void ShowLoading(string message);
        void HideLoading();
        void ShowBlockingError(string message, Action retry, Action startNew);
        void HideBlockingError();
        void ShowSaveWarning(string message, Action retrySave);
        void HideSaveWarning();
        void SetInputBlocked(bool isBlocked);
    }
}
