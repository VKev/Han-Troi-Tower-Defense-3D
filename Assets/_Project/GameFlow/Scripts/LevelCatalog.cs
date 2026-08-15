using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.GameFlow
{
    [Serializable]
    public sealed class LevelCatalogEntry
    {
        [SerializeField, Min(1)] private int levelNumber = 1;
        [SerializeField] private string displayName = "Level 1";
        [SerializeField] private string scenePath = string.Empty;

        public LevelCatalogEntry()
        {
        }

        public LevelCatalogEntry(int levelNumber, string displayName, string scenePath)
        {
            this.levelNumber = levelNumber;
            this.displayName = displayName ?? string.Empty;
            this.scenePath = scenePath ?? string.Empty;
        }

        public int LevelNumber => levelNumber;
        public string DisplayName => displayName;
        public string ScenePath => scenePath;

        public bool HasFullScenePath =>
            !string.IsNullOrWhiteSpace(scenePath)
            && scenePath.StartsWith("Assets/", StringComparison.Ordinal)
            && scenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);
    }

    [CreateAssetMenu(fileName = "LevelCatalog", menuName = "Tower Defense/Game Flow/Level Catalog")]
    public sealed class LevelCatalog : ScriptableObject
    {
        [SerializeField] private List<LevelCatalogEntry> levels = new List<LevelCatalogEntry>();

        public IReadOnlyList<LevelCatalogEntry> Levels => levels;

        public bool TryGetLevel(int levelNumber, out LevelCatalogEntry entry)
        {
            for (int index = 0; index < levels.Count; index++)
            {
                LevelCatalogEntry candidate = levels[index];
                if (candidate != null && candidate.LevelNumber == levelNumber)
                {
                    entry = candidate;
                    return true;
                }
            }

            entry = null;
            return false;
        }

        public List<LevelCatalogEntry> CreateOrderedSnapshot()
        {
            var snapshot = new List<LevelCatalogEntry>(levels.Count);
            for (int index = 0; index < levels.Count; index++)
            {
                LevelCatalogEntry entry = levels[index];
                if (entry != null)
                {
                    snapshot.Add(entry);
                }
            }

            snapshot.Sort((left, right) => left.LevelNumber.CompareTo(right.LevelNumber));
            return snapshot;
        }

        public bool TryValidate(out string error)
        {
            if (levels == null || levels.Count == 0)
            {
                error = "Level Catalog requires at least one level.";
                return false;
            }

            var numbers = new HashSet<int>();
            var paths = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < levels.Count; index++)
            {
                LevelCatalogEntry entry = levels[index];
                if (entry == null)
                {
                    error = $"Level Catalog entry {index} is missing.";
                    return false;
                }

                if (entry.LevelNumber <= 0)
                {
                    error = $"Level number at index {index} must be positive.";
                    return false;
                }

                if (!numbers.Add(entry.LevelNumber))
                {
                    error = $"Level number {entry.LevelNumber} is duplicated.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(entry.DisplayName))
                {
                    error = $"Level {entry.LevelNumber} requires a display name.";
                    return false;
                }

                if (!entry.HasFullScenePath)
                {
                    error = $"Level {entry.LevelNumber} requires a full Assets/.../*.unity scene path.";
                    return false;
                }

                if (!paths.Add(entry.ScenePath))
                {
                    error = $"Scene path '{entry.ScenePath}' is duplicated.";
                    return false;
                }
            }

            if (!numbers.Contains(1))
            {
                error = "Level Catalog must contain Level 1.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
