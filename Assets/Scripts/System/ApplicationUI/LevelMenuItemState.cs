using System;

namespace TowerDefense3D.GameFlow
{
    public readonly struct LevelMenuItemState
    {
        public LevelMenuItemState(
            int levelNumber,
            string displayName,
            bool isUnlocked,
            bool isCleared,
            bool isBusy)
        {
            LevelNumber = levelNumber;
            DisplayName = displayName ?? string.Empty;
            IsUnlocked = isUnlocked;
            IsCleared = isCleared;
            IsBusy = isBusy;
        }

        public int LevelNumber { get; }
        public string DisplayName { get; }
        public bool IsUnlocked { get; }

        /// <summary>
        /// Whether the player has actually beaten this level, which is not the same as having
        /// reached it: a level can be open and still unbeaten. The journey nodes draw those two
        /// as different colours, so the menu has to carry them separately.
        /// </summary>
        public bool IsCleared { get; }

        public bool IsBusy { get; }
    }
}
