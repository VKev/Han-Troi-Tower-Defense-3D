using System;
using UnityEngine;

namespace TowerDefense3D.GameFlow
{
    [Serializable]
    public sealed class SaveRootV1
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

        public static SaveRootV1 Create(int[] unlockedLevelNumbers, string savedAtUtc, string appVersion)
        {
            return new SaveRootV1
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

    public enum SaveLoadStatus
    {
        Success,
        Missing,
        Corrupt,
        Incompatible,
        Unavailable,
        Unexpected
    }

    public readonly struct SaveLoadResult
    {
        public SaveLoadResult(SaveLoadStatus status, SaveRootV1 data, string error)
        {
            Status = status;
            Data = data;
            Error = error ?? string.Empty;
        }

        public SaveLoadStatus Status { get; }
        public SaveRootV1 Data { get; }
        public string Error { get; }
        public bool IsSuccess => Status == SaveLoadStatus.Success && Data != null;
    }

    public enum SaveWriteStatus
    {
        Success,
        ValidationFailed,
        Unavailable,
        Unexpected
    }

    public readonly struct SaveWriteResult
    {
        public SaveWriteResult(SaveWriteStatus status, string error)
        {
            Status = status;
            Error = error ?? string.Empty;
        }

        public SaveWriteStatus Status { get; }
        public string Error { get; }
        public bool IsSuccess => Status == SaveWriteStatus.Success;
    }
}
