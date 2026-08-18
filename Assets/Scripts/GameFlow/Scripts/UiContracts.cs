using System;
using System.Collections.Generic;

namespace TowerDefense3D.GameFlow
{
    public readonly struct LevelMenuItemState
    {
        public LevelMenuItemState(int levelNumber, string displayName, bool isUnlocked, bool isBusy)
        {
            LevelNumber = levelNumber;
            DisplayName = displayName ?? string.Empty;
            IsUnlocked = isUnlocked;
            IsBusy = isBusy;
        }

        public int LevelNumber { get; }
        public string DisplayName { get; }
        public bool IsUnlocked { get; }
        public bool IsBusy { get; }
    }

    /// <summary>
    /// Presentation boundary consumed by the application flow coordinator.
    /// Implementations render state and publish commands; they do not load scenes or access save files.
    /// </summary>
    public interface IApplicationUI
    {
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
