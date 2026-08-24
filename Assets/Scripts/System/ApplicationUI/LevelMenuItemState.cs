using System;

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
}
