using System;
using UnityEngine;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// One level's best star score, as it sits on disk.
    /// </summary>
    /// <remarks>
    /// A pair per level rather than one array indexed by level number: the journey is authored
    /// as a catalog and its numbers need not be contiguous, so an indexed array would either
    /// carry holes or quietly mis-attribute a score the moment a level is inserted.
    /// </remarks>
    [Serializable]
    public struct LevelStarRecord
    {
        [SerializeField] private int levelNumber;
        [SerializeField] private int stars;

        public LevelStarRecord(int levelNumber, int stars)
        {
            this.levelNumber = levelNumber;
            this.stars = stars;
        }

        public int LevelNumber => levelNumber;
        public int Stars => stars;
    }
}
