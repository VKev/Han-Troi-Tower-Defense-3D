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

        // Levels 1 and 2 used to score full marks whatever health the run ended with, because
        // the wave 3 tutorial leaked on purpose and scoring those levels on health would have
        // docked the player for following the script. That leak is gone, so the exemption went
        // with it: every level is scored on the health it ends with, and the gold a level pays
        // follows that same score.

        /// <summary>
        /// What <paramref name="stars"/> on a level worth <paramref name="fullStarGoldReward"/>
        /// is cumulatively worth: a third of the purse per star.
        /// </summary>
        /// <remarks>
        /// Cumulative rather than per-star on purpose. A replay is paid the difference between
        /// what its new score is worth and what the old score already earned, so a player who
        /// takes three runs to reach three stars is paid exactly what a player who managed it
        /// first time was - no more, and no less.
        ///
        /// Scaled before dividing, so a purse that does not divide by three - 250, say - still
        /// pays out to exactly 250 at three stars instead of losing change at every step.
        /// </remarks>
        public static int GoldForStars(int fullStarGoldReward, int stars)
        {
            if (fullStarGoldReward <= 0 || stars <= NoStars)
            {
                return 0;
            }

            return stars >= MaximumStars
                ? fullStarGoldReward
                : fullStarGoldReward * stars / MaximumStars;
        }

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
