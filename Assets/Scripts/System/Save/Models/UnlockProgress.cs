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
    ///
    /// Clearing is tracked separately from unlocking: the level menu unlocks a level the
    /// moment it is selected, so only a recorded victory says the player actually beat it.
    /// Content gated behind "beat level N" - hero towers, for instance - reads the cleared set.
    /// </summary>
    public sealed class UnlockProgress
    {
        public const int InitiallyUnlockedLevel = 1;

        private readonly HashSet<int> unlockedLevels = new HashSet<int>();
        private readonly HashSet<int> clearedLevels = new HashSet<int>();

        public UnlockProgress()
            : this(null, null)
        {
        }

        public UnlockProgress(IEnumerable<int> restoredLevels)
            : this(restoredLevels, null)
        {
        }

        public UnlockProgress(IEnumerable<int> restoredLevels, IEnumerable<int> restoredClearedLevels)
        {
            unlockedLevels.Add(InitiallyUnlockedLevel);
            AddPositiveLevels(restoredLevels, unlockedLevels);
            AddPositiveLevels(restoredClearedLevels, clearedLevels);
        }

        public int Count => unlockedLevels.Count;
        public int ClearedCount => clearedLevels.Count;

        public bool IsUnlocked(int levelNumber)
        {
            return unlockedLevels.Contains(levelNumber);
        }

        public bool IsCleared(int levelNumber)
        {
            return clearedLevels.Contains(levelNumber);
        }

        /// <summary>
        /// Records one level as beaten. Clearing a level also leaves it unlocked, because a
        /// level can only be cleared by playing it.
        /// </summary>
        public UnlockAttemptResult TryMarkCleared(int levelNumber)
        {
            if (levelNumber <= 0)
            {
                return UnlockAttemptResult.InvalidLevel;
            }

            unlockedLevels.Add(levelNumber);
            return clearedLevels.Add(levelNumber)
                ? UnlockAttemptResult.Unlocked
                : UnlockAttemptResult.AlreadyUnlocked;
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
            return CreateSortedSnapshot(unlockedLevels);
        }

        public int[] CreateSortedClearedSnapshot()
        {
            return CreateSortedSnapshot(clearedLevels);
        }

        private static int[] CreateSortedSnapshot(HashSet<int> levels)
        {
            var snapshot = new int[levels.Count];
            levels.CopyTo(snapshot);
            System.Array.Sort(snapshot);
            return snapshot;
        }

        private static void AddPositiveLevels(IEnumerable<int> source, HashSet<int> destination)
        {
            if (source == null)
            {
                return;
            }

            foreach (int levelNumber in source)
            {
                if (levelNumber > 0)
                {
                    destination.Add(levelNumber);
                }
            }
        }
    }
}
