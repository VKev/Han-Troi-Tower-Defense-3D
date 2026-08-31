using System;
using UnityEngine;

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

        public int SchemaVersion => schemaVersion;
        public string SlotId => slotId;
        public string SavedAtUtc => savedAtUtc;
        public string AppVersion => appVersion;
        public int[] UnlockedLevelNumbers => unlockedLevelNumbers;
        public int[] ClearedLevelNumbers => clearedLevelNumbers ?? Array.Empty<int>();

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
            return new SaveSnapshot
            {
                schemaVersion = CurrentSchemaVersion,
                slotId = AutosaveSlotId,
                savedAtUtc = savedAtUtc ?? string.Empty,
                appVersion = appVersion ?? string.Empty,
                unlockedLevelNumbers = unlockedLevelNumbers ?? Array.Empty<int>(),
                clearedLevelNumbers = clearedLevelNumbers ?? Array.Empty<int>()
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

            error = string.Empty;
            return true;
        }
    }
}
