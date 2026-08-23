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

        public int SchemaVersion => schemaVersion;
        public string SlotId => slotId;
        public string SavedAtUtc => savedAtUtc;
        public string AppVersion => appVersion;
        public int[] UnlockedLevelNumbers => unlockedLevelNumbers;

        public static SaveSnapshot Create(int[] unlockedLevelNumbers, string savedAtUtc, string appVersion)
        {
            return new SaveSnapshot
            {
                schemaVersion = CurrentSchemaVersion,
                slotId = AutosaveSlotId,
                savedAtUtc = savedAtUtc ?? string.Empty,
                appVersion = appVersion ?? string.Empty,
                unlockedLevelNumbers = unlockedLevelNumbers ?? Array.Empty<int>()
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

            error = string.Empty;
            return true;
        }
    }
}
