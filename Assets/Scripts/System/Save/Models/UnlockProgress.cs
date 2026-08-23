using System.Collections.Generic;

namespace TowerDefense3D.GameFlow
{
    public enum UnlockAttemptResult
    {
        InvalidLevel,
        AlreadyUnlocked,
        Unlocked
    }

    /// <summary>
    /// Authoritative in-memory unlocked-level state. Level 1 is always unlocked.
    /// Unlocking a selected level never implies unlocking any other level.
    /// </summary>
    public sealed class UnlockProgress
    {
        public const int InitiallyUnlockedLevel = 1;

        private readonly HashSet<int> unlockedLevels = new HashSet<int>();

        public UnlockProgress()
            : this(null)
        {
        }

        public UnlockProgress(IEnumerable<int> restoredLevels)
        {
            unlockedLevels.Add(InitiallyUnlockedLevel);
            if (restoredLevels == null)
            {
                return;
            }

            foreach (int levelNumber in restoredLevels)
            {
                if (levelNumber > 0)
                {
                    unlockedLevels.Add(levelNumber);
                }
            }
        }

        public int Count => unlockedLevels.Count;

        public bool IsUnlocked(int levelNumber)
        {
            return unlockedLevels.Contains(levelNumber);
        }

        public UnlockAttemptResult TryUnlock(int levelNumber)
        {
            if (levelNumber <= 0)
            {
                return UnlockAttemptResult.InvalidLevel;
            }

            return unlockedLevels.Add(levelNumber)
                ? UnlockAttemptResult.Unlocked
                : UnlockAttemptResult.AlreadyUnlocked;
        }

        public int[] CreateSortedSnapshot()
        {
            var snapshot = new int[unlockedLevels.Count];
            unlockedLevels.CopyTo(snapshot);
            System.Array.Sort(snapshot);
            return snapshot;
        }
    }
}
