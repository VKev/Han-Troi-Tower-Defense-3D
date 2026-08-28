using System;
using UnityEngine;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Authored identity and scene path for one playable level.
    /// </summary>
    [Serializable]
    public sealed class LevelCatalogEntry
    {
        [SerializeField, Min(1)] private int levelNumber = 1;
        [SerializeField] private string displayName = "Level 1";
        [SerializeField] private string scenePath = string.Empty;
        [SerializeField, Min(0)] private int startingGold = 400;
        [SerializeField, Min(1)] private int startingHealth = 10;

        public int StartingGold => startingGold;
        public int StartingHealth => startingHealth;

        public LevelCatalogEntry()
        {
        }

        public LevelCatalogEntry(int levelNumber, string displayName, string scenePath)
            : this(levelNumber, displayName, scenePath, 400, 10)
        {
        }

        public LevelCatalogEntry(
            int levelNumber,
            string displayName,
            string scenePath,
            int startingGold,
            int startingHealth)
        {
            this.levelNumber = levelNumber;
            this.displayName = displayName ?? string.Empty;
            this.scenePath = scenePath ?? string.Empty;
            this.startingGold = startingGold;
            this.startingHealth = startingHealth;
        }

        public int LevelNumber => levelNumber;
        public string DisplayName => displayName;
        public string ScenePath => scenePath;

        public bool HasFullScenePath =>
            !string.IsNullOrWhiteSpace(scenePath)
            && scenePath.StartsWith("Assets/", StringComparison.Ordinal)
            && scenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);
    }
}
