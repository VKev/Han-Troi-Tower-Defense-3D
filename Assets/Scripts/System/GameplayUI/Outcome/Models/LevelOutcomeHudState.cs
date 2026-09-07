using UnityEngine;

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

    /// <summary>
    /// Everything the outcome panel draws.
    /// </summary>
    /// <remarks>
    /// The health and the gold are carried as numbers rather than as the one pre-formatted
    /// summary line this used to hold. The panel draws the Cóc's health as a bar with the
    /// two-star threshold marked on it, which a string cannot be turned back into, and the star
    /// score has to come across whole because the panel is where the player is told what the run
    /// scored.
    /// </remarks>
    public readonly struct LevelOutcomeHudState
    {
        public LevelOutcomeHudState(
            bool isVisible,
            LevelOutcome outcome,
            string titleText,
            int stars,
            int currentHealth,
            int maximumHealth,
            int gold,
            bool nextLevelVisible)
        {
            IsVisible = isVisible;
            Outcome = outcome;
            TitleText = titleText;
            Stars = stars;
            CurrentHealth = currentHealth;
            MaximumHealth = maximumHealth;
            Gold = gold;
            NextLevelVisible = nextLevelVisible;
        }

        public bool IsVisible { get; }
        public LevelOutcome Outcome { get; }
        public string TitleText { get; }

        /// <summary>What this attempt scored, nought to three. A defeat scores nothing.</summary>
        public int Stars { get; }

        public int CurrentHealth { get; }
        public int MaximumHealth { get; }
        public int Gold { get; }
        public bool NextLevelVisible { get; }

        /// <summary>
        /// How full the health bar is drawn. Computed here rather than by the view, so the bar
        /// and anything else that reads the ratio cannot disagree about a level authored with
        /// no health at all.
        /// </summary>
        public float HealthRatio => MaximumHealth <= 0
            ? 0f
            : Mathf.Clamp01(CurrentHealth / (float)MaximumHealth);
    }
}
