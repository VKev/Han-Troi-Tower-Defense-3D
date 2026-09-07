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
    ///
    /// A cleared level also carries a star score, which is the best any attempt has managed
    /// rather than the latest. Replaying a beaten level to try for a cleaner run is the point
    /// of the score, and a sloppy replay must not take back what an earlier run earned.
    /// </summary>
    public sealed class UnlockProgress
    {
        public const int InitiallyUnlockedLevel = 1;

        private readonly HashSet<int> unlockedLevels = new HashSet<int>();
        private readonly HashSet<int> clearedLevels = new HashSet<int>();
        private readonly Dictionary<int, int> levelStars = new Dictionary<int, int>();

        public UnlockProgress()
            : this(null, null, null)
        {
        }

        public UnlockProgress(IEnumerable<int> restoredLevels)
            : this(restoredLevels, null, null)
        {
        }

        public UnlockProgress(IEnumerable<int> restoredLevels, IEnumerable<int> restoredClearedLevels)
            : this(restoredLevels, restoredClearedLevels, null)
        {
        }

        public UnlockProgress(
            IEnumerable<int> restoredLevels,
            IEnumerable<int> restoredClearedLevels,
            IEnumerable<LevelStarRecord> restoredStars)
        {
            unlockedLevels.Add(InitiallyUnlockedLevel);
            AddPositiveLevels(restoredLevels, unlockedLevels);
            AddPositiveLevels(restoredClearedLevels, clearedLevels);
            AddStars(restoredStars);
        }

        public int Count => unlockedLevels.Count;
        public int ClearedCount => clearedLevels.Count;

        /// <summary>Every star earned across the journey, which is what the top bar counts.</summary>
        public int TotalStars
        {
            get
            {
                int total = 0;
                foreach (KeyValuePair<int, int> entry in levelStars)
                {
                    total += entry.Value;
                }

                return total;
            }
        }

        public bool IsUnlocked(int levelNumber)
        {
            return unlockedLevels.Contains(levelNumber);
        }

        public bool IsCleared(int levelNumber)
        {
            return clearedLevels.Contains(levelNumber);
        }

        /// <summary>
        /// The best score recorded for one level, or none for a level never beaten.
        /// </summary>
        public int GetStars(int levelNumber)
        {
            return levelStars.TryGetValue(levelNumber, out int stars)
                ? stars
                : LevelStarRating.NoStars;
        }

        /// <summary>
        /// Records one level as beaten at <paramref name="stars"/>. Clearing a level also leaves
        /// it unlocked, because a level can only be cleared by playing it.
        /// </summary>
        /// <returns>
        /// <see cref="UnlockAttemptResult.Unlocked"/> when this call changed something worth
        /// persisting - a first clear, or a better score than the one on record.
        /// </returns>
        public UnlockAttemptResult TryMarkCleared(int levelNumber, int stars)
        {
            if (levelNumber <= 0)
            {
                return UnlockAttemptResult.InvalidLevel;
            }

            unlockedLevels.Add(levelNumber);
            bool isFirstClear = clearedLevels.Add(levelNumber);

            // Only an improvement is written. A replay that goes badly leaves the level's best
            // run standing, and costs no save.
            int best = GetStars(levelNumber);
            bool isBetterScore = stars > best;
            if (isBetterScore)
            {
                levelStars[levelNumber] = stars;
            }

            return isFirstClear || isBetterScore
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

        /// <summary>
        /// Every recorded score, in level order so two saves of the same progress are the same
        /// bytes and a diff of the file says something.
        /// </summary>
        public LevelStarRecord[] CreateSortedStarSnapshot()
        {
            var levels = new int[levelStars.Count];
            levelStars.Keys.CopyTo(levels, 0);
            System.Array.Sort(levels);

            var snapshot = new LevelStarRecord[levels.Length];
            for (int index = 0; index < levels.Length; index++)
            {
                snapshot[index] = new LevelStarRecord(levels[index], levelStars[levels[index]]);
            }

            return snapshot;
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

        /// <summary>
        /// Restores scores from disk, dropping anything a hand-edited or corrupted save could
        /// carry: a score for a level that was never beaten, and any count outside the scale.
        /// </summary>
        private void AddStars(IEnumerable<LevelStarRecord> source)
        {
            if (source == null)
            {
                return;
            }

            foreach (LevelStarRecord record in source)
            {
                if (record.LevelNumber <= 0
                    || record.Stars <= LevelStarRating.NoStars
                    || record.Stars > LevelStarRating.MaximumStars
                    || !clearedLevels.Contains(record.LevelNumber))
                {
                    continue;
                }

                levelStars[record.LevelNumber] = record.Stars;
            }
        }
    }
}
