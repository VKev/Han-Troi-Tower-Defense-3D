using System;
using TowerDefense3D.Economy;
using TowerDefense3D.Waves;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Shows one outcome panel when a level attempt ends - victory once every authored
    /// wave is cleared, defeat once the Cóc runs out of HP - and routes its replay,
    /// next-level, and return-to-menu commands back to the application flow.
    /// </summary>
    public sealed class LevelOutcomeHudPresenter
    {
        private readonly IWaveSystem waveSystem;
        private readonly LevelGoldSystem goldSystem;
        private readonly LevelBaseHealthSystem healthSystem;
        private readonly ILevelOutcomeHudView view;

        private string levelDisplayName = string.Empty;
        private bool hasNextLevel;
        private Action requestReplayLevel;
        private Action requestNextLevel;
        private Action requestReturnToLevelMenu;
        private Action reportLevelCleared;
        private bool hasReportedLevelCleared;

        public LevelOutcomeHudPresenter(
            IWaveSystem waveSystem,
            LevelGoldSystem goldSystem,
            LevelBaseHealthSystem healthSystem,
            ILevelOutcomeHudView view)
        {
            this.waveSystem = waveSystem ?? throw new ArgumentNullException(nameof(waveSystem));
            this.goldSystem = goldSystem ?? throw new ArgumentNullException(nameof(goldSystem));
            this.healthSystem = healthSystem ?? throw new ArgumentNullException(nameof(healthSystem));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
        }

        public void BindLevel(
            string levelDisplayName,
            bool hasNextLevel,
            Action requestReplayLevel,
            Action requestNextLevel,
            Action requestReturnToLevelMenu)
        {
            BindLevel(
                levelDisplayName,
                hasNextLevel,
                requestReplayLevel,
                requestNextLevel,
                requestReturnToLevelMenu,
                null);
        }

        /// <summary>
        /// <paramref name="reportLevelCleared"/> fires once, the first time this attempt reaches
        /// victory, so progression gated behind "beat this level" can be recorded.
        /// </summary>
        public void BindLevel(
            string levelDisplayName,
            bool hasNextLevel,
            Action requestReplayLevel,
            Action requestNextLevel,
            Action requestReturnToLevelMenu,
            Action reportLevelCleared)
        {
            this.levelDisplayName = levelDisplayName ?? string.Empty;
            this.hasNextLevel = hasNextLevel;
            this.reportLevelCleared = reportLevelCleared;
            hasReportedLevelCleared = false;
            this.requestReplayLevel = requestReplayLevel
                ?? throw new ArgumentNullException(nameof(requestReplayLevel));
            this.requestNextLevel = requestNextLevel
                ?? throw new ArgumentNullException(nameof(requestNextLevel));
            this.requestReturnToLevelMenu = requestReturnToLevelMenu
                ?? throw new ArgumentNullException(nameof(requestReturnToLevelMenu));
        }

        public void Connect()
        {
            view.Initialize();
            view.PlayAgainRequested += HandlePlayAgainRequested;
            view.NextLevelRequested += HandleNextLevelRequested;
            view.ReturnToLevelMenuRequested += HandleReturnToLevelMenuRequested;
            Refresh();
        }

        public void Disconnect()
        {
            view.PlayAgainRequested -= HandlePlayAgainRequested;
            view.NextLevelRequested -= HandleNextLevelRequested;
            view.ReturnToLevelMenuRequested -= HandleReturnToLevelMenuRequested;
            view.Shutdown();
        }

        public void Refresh()
        {
            WavePhase phase = waveSystem.CreateState().Phase;
            if (phase != WavePhase.Victory && phase != WavePhase.Defeat)
            {
                view.Render(new LevelOutcomeHudState(
                    false,
                    LevelOutcome.Victory,
                    string.Empty,
                    string.Empty,
                    false));
                return;
            }

            bool isVictory = phase == WavePhase.Victory;
            if (isVictory && !hasReportedLevelCleared)
            {
                hasReportedLevelCleared = true;
                reportLevelCleared?.Invoke();
            }

            view.Render(new LevelOutcomeHudState(
                true,
                isVictory ? LevelOutcome.Victory : LevelOutcome.Defeat,
                isVictory ? "VICTORY" : "DEFEAT",
                CreateSummaryText(isVictory),
                isVictory && hasNextLevel));
        }

        private string CreateSummaryText(bool isVictory)
        {
            string headline = CreateHeadline(isVictory);
            return $"{headline}   •   CÓC HP {healthSystem.CurrentHealth}/{healthSystem.MaximumHealth}"
                + $"   •   GOLD {goldSystem.Balance:N0}";
        }

        private string CreateHeadline(bool isVictory)
        {
            if (string.IsNullOrWhiteSpace(levelDisplayName))
            {
                return isVictory ? "All waves cleared" : "The Cóc has fallen";
            }

            return isVictory
                ? levelDisplayName + " cleared"
                : levelDisplayName + " lost";
        }

        private void HandlePlayAgainRequested()
        {
            requestReplayLevel?.Invoke();
        }

        private void HandleNextLevelRequested()
        {
            if (hasNextLevel && waveSystem.CreateState().Phase == WavePhase.Victory)
            {
                requestNextLevel?.Invoke();
            }
        }

        private void HandleReturnToLevelMenuRequested()
        {
            requestReturnToLevelMenu?.Invoke();
        }
    }
}
