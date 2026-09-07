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
