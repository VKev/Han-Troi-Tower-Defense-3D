namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Turns how much of the Cóc's health survived a level into that attempt's star score.
    /// Three stars is a run the Cóc came through untouched, two is a run it came through with
    /// at least half its health, one is anything scrappier that still ended in a victory.
    /// </summary>
    /// <remarks>
    /// The comparison is done in whole numbers rather than on a health ratio: a level authored
    /// with an odd starting health - 25, say - has no exact half, and a float ratio would hand
    /// out three stars or two on the strength of a rounding error. Doubling the survivor instead
    /// makes "at least half" exact at every starting health.
    /// </remarks>
    public static class LevelStarRating
    {
        public const int MaximumStars = 3;

        /// <summary>What an attempt that was not won scores.</summary>
        public const int NoStars = 0;

        private const int TutorialLevelCount = 2;

        /// <summary>
        /// Whether clearing this level scores full marks regardless of what the C\u00f3c had left.
        /// </summary>
        /// <remarks>
        /// Levels 1 and 2 are the tutorial, and they damage the C\u00f3c on purpose: the wave 3 leak
        /// is the lesson. Rating them on surviving health would dock the player for following
        /// the script, so finishing them at all is worth three stars.
        /// </remarks>
        public static bool AwardsFullStars(int levelNumber) => levelNumber <= TutorialLevelCount;

        public static int FromRemainingHealth(int currentHealth, int maximumHealth)
        {
            // A level cannot be cleared by a fallen Cóc, so a run with nothing left scores
            // nothing at all rather than the one star a bare victory earns.
            if (currentHealth <= 0 || maximumHealth <= 0)
            {
                return NoStars;
            }

            if (currentHealth >= maximumHealth)
            {
                return MaximumStars;
            }

            return currentHealth * 2 >= maximumHealth ? 2 : 1;
        }
    }
}
