namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// What the run the player has just come back from earned them, so the journey screen can
    /// show it arriving rather than simply having a bigger number than it did before.
    /// </summary>
    /// <remarks>
    /// Only what is <em>new</em> is carried. A replay of a level already at three stars earns
    /// nothing and reports nothing, which is what makes <see cref="HasAnything"/> the whole of
    /// the decision about whether the menu animates at all.
    ///
    /// Payouts accumulate across levels beaten in one sitting - beat three levels in a row from
    /// the victory panel and every star and coin is still counted - but only the last level's
    /// node is remembered, because that is the one the player is arriving from. Flying the whole
    /// haul out of that node is a small lie and a much simpler one than replaying three separate
    /// flights nobody asked to watch.
    /// </remarks>
    public readonly struct LevelMenuRewardState
    {
        public LevelMenuRewardState(int levelNumber, int starsGained, int goldGained)
        {
            LevelNumber = levelNumber;
            StarsGained = starsGained > 0 ? starsGained : 0;
            GoldGained = goldGained > 0 ? goldGained : 0;
        }

        /// <summary>The node the reward flies out of: the level the player last beat.</summary>
        public int LevelNumber { get; }

        /// <summary>Stars this run added to the level's best, not the level's total.</summary>
        public int StarsGained { get; }

        /// <summary>Gold this run paid into the wallet.</summary>
        public int GoldGained { get; }

        public bool HasAnything => StarsGained > 0 || GoldGained > 0;

        /// <summary>Nothing to celebrate: the menu opens with its totals already settled.</summary>
        public static LevelMenuRewardState None => default;

        /// <summary>
        /// Folds another payout in, keeping <paramref name="other"/>'s level as the origin.
        /// </summary>
        public LevelMenuRewardState Add(LevelMenuRewardState other)
        {
            if (!other.HasAnything)
            {
                return this;
            }

            return new LevelMenuRewardState(
                other.LevelNumber,
                StarsGained + other.StarsGained,
                GoldGained + other.GoldGained);
        }
    }
}
