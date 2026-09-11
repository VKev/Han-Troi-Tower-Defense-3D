using System;
using UnityEngine;
using TowerDefense3D.Tutorials;

namespace TowerDefense3D.GameFlow
{
    [Serializable]
    public sealed class SaveSnapshot
    {
        public const int CurrentSchemaVersion = 1;
        public const string AutosaveSlotId = "autosave";

        [SerializeField] private int schemaVersion = CurrentSchemaVersion;
        [SerializeField] private string slotId = AutosaveSlotId;
        [SerializeField] private string savedAtUtc = string.Empty;
        [SerializeField] private string appVersion = string.Empty;
        [SerializeField] private int[] unlockedLevelNumbers = { UnlockProgress.InitiallyUnlockedLevel };

        // Added after the first shipped saves, so a save written without it deserializes this
        // as null and simply reports no level as cleared yet. That keeps schema version 1.
        [SerializeField] private int[] clearedLevelNumbers = Array.Empty<int>();

        // Same story as the cleared set: a save written before scoring existed deserializes this
        // as null and reports no score for any level, which is exactly what an old save means.
        [SerializeField] private LevelStarRecord[] levelStars = Array.Empty<LevelStarRecord>();
        [SerializeField] private TutorialSaveRecord[] tutorials = Array.Empty<TutorialSaveRecord>();
        [SerializeField] private string[] discoveredEnemyIds = Array.Empty<string>();

        public int SchemaVersion => schemaVersion;
        public string SlotId => slotId;
        public string SavedAtUtc => savedAtUtc;
        public string AppVersion => appVersion;
        public int[] UnlockedLevelNumbers => unlockedLevelNumbers;
        public int[] ClearedLevelNumbers => clearedLevelNumbers ?? Array.Empty<int>();
        public LevelStarRecord[] LevelStars => levelStars ?? Array.Empty<LevelStarRecord>();
        public TutorialSaveRecord[] Tutorials => tutorials ?? Array.Empty<TutorialSaveRecord>();
        public string[] DiscoveredEnemyIds => discoveredEnemyIds ?? Array.Empty<string>();

        public static SaveSnapshot Create(int[] unlockedLevelNumbers, string savedAtUtc, string appVersion)
        {
            return Create(unlockedLevelNumbers, Array.Empty<int>(), savedAtUtc, appVersion);
        }

        public static SaveSnapshot Create(
            int[] unlockedLevelNumbers,
            int[] clearedLevelNumbers,
            string savedAtUtc,
            string appVersion)
        {
            return Create(
                unlockedLevelNumbers,
                clearedLevelNumbers,
                Array.Empty<LevelStarRecord>(),
                Array.Empty<TutorialSaveRecord>(),
                savedAtUtc,
                appVersion);
        }

        public static SaveSnapshot Create(
            int[] unlockedLevelNumbers,
            int[] clearedLevelNumbers,
            LevelStarRecord[] levelStars,
            string savedAtUtc,
            string appVersion)
        {
            return Create(
                unlockedLevelNumbers,
                clearedLevelNumbers,
                levelStars,
                Array.Empty<TutorialSaveRecord>(),
                savedAtUtc,
                appVersion);
        }

        public static SaveSnapshot Create(
            int[] unlockedLevelNumbers,
            int[] clearedLevelNumbers,
            LevelStarRecord[] levelStars,
            TutorialSaveRecord[] tutorials,
            string savedAtUtc,
            string appVersion)
        {
            return Create(
                unlockedLevelNumbers,
                clearedLevelNumbers,
                levelStars,
                tutorials,
                Array.Empty<string>(),
                savedAtUtc,
                appVersion);
        }

        public static SaveSnapshot Create(
            int[] unlockedLevelNumbers,
            int[] clearedLevelNumbers,
            LevelStarRecord[] levelStars,
            TutorialSaveRecord[] tutorials,
            string[] discoveredEnemyIds,
            string savedAtUtc,
            string appVersion)
        {
            return new SaveSnapshot
            {
                schemaVersion = CurrentSchemaVersion,
                slotId = AutosaveSlotId,
                savedAtUtc = savedAtUtc ?? string.Empty,
                appVersion = appVersion ?? string.Empty,
                unlockedLevelNumbers = unlockedLevelNumbers ?? Array.Empty<int>(),
                clearedLevelNumbers = clearedLevelNumbers ?? Array.Empty<int>(),
                levelStars = levelStars ?? Array.Empty<LevelStarRecord>(),
                tutorials = tutorials ?? Array.Empty<TutorialSaveRecord>(),
                discoveredEnemyIds = discoveredEnemyIds ?? Array.Empty<string>()
            };
        }

        public bool TryValidate(out string error)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                error = $"Unsupported save schema {schemaVersion}.";
                return false;
            }

            if (!string.Equals(slotId, AutosaveSlotId, StringComparison.Ordinal))
            {
                error = "Save slot identifier is invalid.";
                return false;
            }

            if (unlockedLevelNumbers == null || unlockedLevelNumbers.Length == 0)
            {
                error = "Save requires at least Level 1 to be unlocked.";
                return false;
            }

            bool hasLevelOne = false;
            for (int index = 0; index < unlockedLevelNumbers.Length; index++)
            {
                int levelNumber = unlockedLevelNumbers[index];
                if (levelNumber <= 0)
                {
                    error = $"Unlocked level number at index {index} must be positive.";
                    return false;
                }

                hasLevelOne |= levelNumber == UnlockProgress.InitiallyUnlockedLevel;
            }

            if (!hasLevelOne)
            {
                error = "Save must include Level 1.";
                return false;
            }

            int[] cleared = ClearedLevelNumbers;
            for (int index = 0; index < cleared.Length; index++)
            {
                if (cleared[index] <= 0)
                {
                    error = $"Cleared level number at index {index} must be positive.";
                    return false;
                }
            }

            LevelStarRecord[] stars = LevelStars;
            for (int index = 0; index < stars.Length; index++)
            {
                if (stars[index].LevelNumber <= 0)
                {
                    error = $"Star record at index {index} names a non-positive level.";
                    return false;
                }

                if (stars[index].Stars < LevelStarRating.NoStars
                    || stars[index].Stars > LevelStarRating.MaximumStars)
                {
                    error = $"Star record at index {index} is outside 0..{LevelStarRating.MaximumStars}.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }
    }
}
