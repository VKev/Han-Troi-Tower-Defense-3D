using System;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Identifies the authored level and full Unity scene path requested by GameFlow.
    /// </summary>
    public readonly struct LevelLoadRequest
    {
        public LevelLoadRequest(int levelNumber, string scenePath)
        {
            LevelNumber = levelNumber;
            ScenePath = scenePath ?? string.Empty;
        }

        public int LevelNumber { get; }
        public string ScenePath { get; }

        public bool IsValid =>
            LevelNumber > 0
            && ScenePath.StartsWith("Assets/", StringComparison.Ordinal)
            && ScenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);
    }
}
