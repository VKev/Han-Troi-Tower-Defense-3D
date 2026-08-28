namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Terminal result of one level attempt.
    /// </summary>
    public enum LevelOutcome
    {
        Victory,
        Defeat
    }

    public readonly struct LevelOutcomeHudState
    {
        public LevelOutcomeHudState(
            bool isVisible,
            LevelOutcome outcome,
            string titleText,
            string summaryText,
            bool nextLevelVisible)
        {
            IsVisible = isVisible;
            Outcome = outcome;
            TitleText = titleText;
            SummaryText = summaryText;
            NextLevelVisible = nextLevelVisible;
        }

        public bool IsVisible { get; }
        public LevelOutcome Outcome { get; }
        public string TitleText { get; }
        public string SummaryText { get; }
        public bool NextLevelVisible { get; }
    }
}
