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
        private readonly HashSet<string> unlockedTowers = new HashSet<string>();
        private int gold;

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
            : this(restoredLevels, restoredClearedLevels, restoredStars, 0)
        {
        }

        public UnlockProgress(
            IEnumerable<int> restoredLevels,
            IEnumerable<int> restoredClearedLevels,
            IEnumerable<LevelStarRecord> restoredStars,
            int restoredGold)
            : this(restoredLevels, restoredClearedLevels, restoredStars, restoredGold, null)
        {
        }

        public UnlockProgress(
            IEnumerable<int> restoredLevels,
            IEnumerable<int> restoredClearedLevels,
            IEnumerable<LevelStarRecord> restoredStars,
            int restoredGold,
            IEnumerable<string> restoredTowers)
        {
            AddTowers(restoredTowers);
            unlockedLevels.Add(InitiallyUnlockedLevel);
            AddPositiveLevels(restoredLevels, unlockedLevels);
            AddPositiveLevels(restoredClearedLevels, clearedLevels);
            AddStars(restoredStars);

            // A negative purse can only come from a hand-edited or corrupted save, and starting
            // the player in debt is a worse answer than starting them at nothing.
            gold = restoredGold > 0 ? restoredGold : 0;
        }

        public int Count => unlockedLevels.Count;
        public int ClearedCount => clearedLevels.Count;

        /// <summary>
        /// The player's wallet, carried between levels and across sessions.
        /// </summary>
        /// <remarks>
        /// Distinct from <see cref="TowerDefense3D.Economy.LevelGoldSystem"/>, which is the
        /// spending money inside one run and is thrown away when the run ends. This is what
        /// clearing levels pays out, and the journey screen counts it in the top bar.
        /// </remarks>
        public int Gold => gold;

        /// <summary>
        /// Pays <paramref name="amount"/> into the wallet.
        /// </summary>
        /// <returns>Whether anything was actually added, which is what decides a save write.</returns>
        public bool TryAddGold(int amount)
        {
            if (amount <= 0)
            {
                return false;
            }

            gold = checked(gold + amount);
            return true;
        }

        /// <summary>
        /// Whether the player has earned the right to build this tower.
        /// </summary>
        /// <remarks>
        /// Owning a tower is progression in its own right, recorded the moment it is earned and
        /// carried from then on. It used to be re-derived from whichever level was loaded, which
        /// meant the same save answered the question differently depending on where it was
        /// asked: a player who had only ever met fire and water would find wind waiting for them
        /// on a Level 1 replay, because Level 1 happens not to be the level that withholds it.
        /// What the player owns cannot depend on where they are standing.
        /// </remarks>
        public bool IsTowerUnlocked(string towerId)
        {
            return !string.IsNullOrWhiteSpace(towerId) && unlockedTowers.Contains(towerId);
        }

        /// <summary>
        /// Records one tower as earned.
        /// </summary>
        /// <returns>Whether this call changed anything, which is what decides a save write.</returns>
        public bool TryUnlockTower(string towerId)
        {
            return !string.IsNullOrWhiteSpace(towerId) && unlockedTowers.Add(towerId);
        }

        /// <summary>Every tower earned, in a stable order so two saves of the same progress are the same bytes.</summary>
        public string[] CreateSortedTowerSnapshot()
        {
            var snapshot = new string[unlockedTowers.Count];
            unlockedTowers.CopyTo(snapshot);
            System.Array.Sort(snapshot, System.StringComparer.Ordinal);
            return snapshot;
        }

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

        private void AddTowers(IEnumerable<string> source)
        {
            if (source == null)
            {
                return;
            }

            foreach (string towerId in source)
            {
                if (!string.IsNullOrWhiteSpace(towerId))
                {
                    unlockedTowers.Add(towerId);
                }
            }
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
