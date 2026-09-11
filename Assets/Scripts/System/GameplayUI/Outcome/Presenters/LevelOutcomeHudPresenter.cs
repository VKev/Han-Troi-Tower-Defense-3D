using System;
using TowerDefense3D.Audio;
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
        private const string VictoryTitle = "CHIẾN THẮNG";
        private const string DefeatTitle = "THẤT BẠI";

        private readonly IWaveSystem waveSystem;
        private readonly LevelGoldSystem goldSystem;
        private readonly LevelBaseHealthSystem healthSystem;
        private readonly ILevelOutcomeHudView view;
        private readonly ILevelVictoryEscapeView victoryEscapeView;
        private readonly ISoundPlayer soundPlayer;
        private readonly LevelPreparationMusicSystem levelMusicSystem;

        private bool hasNextLevel;
        private bool awardsFullStars;
        private Action requestReplayLevel;
        private Action requestNextLevel;
        private Action requestReturnToLevelMenu;
        private Action<int> reportLevelCleared;
        private bool hasReportedLevelCleared;
        private bool hasStartedVictoryEscape;
        private bool hasCompletedVictoryEscape;
        private bool hasPlayedOutcomeSound;

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
            soundPlayer = null;
        }

        public LevelOutcomeHudPresenter(
            IWaveSystem waveSystem,
            LevelGoldSystem goldSystem,
            LevelBaseHealthSystem healthSystem,
            ILevelOutcomeHudView view,
            ILevelVictoryEscapeView victoryEscapeView,
            ISoundPlayer soundPlayer = null,
            LevelPreparationMusicSystem levelMusicSystem = null)
            : this(waveSystem, goldSystem, healthSystem, view)
        {
            this.victoryEscapeView = victoryEscapeView
                ?? throw new ArgumentNullException(nameof(victoryEscapeView));
            this.soundPlayer = soundPlayer;
            this.levelMusicSystem = levelMusicSystem;
        }

        public void BindLevel(
            bool hasNextLevel,
            Action requestReplayLevel,
            Action requestNextLevel,
            Action requestReturnToLevelMenu)
        {
            BindLevel(
                hasNextLevel,
                requestReplayLevel,
                requestNextLevel,
                requestReturnToLevelMenu,
                null);
        }

        /// <summary>
        /// <paramref name="reportLevelCleared"/> fires once, the first time this attempt reaches
        /// victory, so progression gated behind "beat this level" can be recorded. It is handed
        /// the attempt's star score, which is read here rather than by the flow it reports to:
        /// the Cóc's health belongs to the level scope and is gone by the time the menu is back.
        /// The call sits after the victory escape has played, and escaping costs the Cóc no
        /// health, so the score is the same one the panel shows.
        /// </summary>
        public void BindLevel(
            bool hasNextLevel,
            Action requestReplayLevel,
            Action requestNextLevel,
            Action requestReturnToLevelMenu,
            Action<int> reportLevelCleared,
            bool awardsFullStars = false)
        {
            this.hasNextLevel = hasNextLevel;
            this.reportLevelCleared = reportLevelCleared;
            this.awardsFullStars = awardsFullStars;
            hasReportedLevelCleared = false;
            hasStartedVictoryEscape = false;
            hasCompletedVictoryEscape = false;
            hasPlayedOutcomeSound = false;
            levelMusicSystem?.SetOutcomeVisible(false);
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
            if (victoryEscapeView != null)
            {
                victoryEscapeView.EscapeCompleted += HandleVictoryEscapeCompleted;
            }

            Refresh();
        }

        public void Disconnect()
        {
            view.PlayAgainRequested -= HandlePlayAgainRequested;
            view.NextLevelRequested -= HandleNextLevelRequested;
            view.ReturnToLevelMenuRequested -= HandleReturnToLevelMenuRequested;
            if (victoryEscapeView != null)
            {
                victoryEscapeView.EscapeCompleted -= HandleVictoryEscapeCompleted;
            }

            view.Shutdown();
        }

        public void Refresh()
        {
            WavePhase phase = waveSystem.CreateState().Phase;
            if (phase == WavePhase.Defeat && waveSystem.IsCurrentWaveRetryAvailable)
            {
                view.Render(Hidden());
                return;
            }

            if (phase != WavePhase.Victory && phase != WavePhase.Defeat)
            {
                view.Render(Hidden());
                return;
            }

            bool isVictory = phase == WavePhase.Victory;
            if (isVictory && victoryEscapeView != null && !hasCompletedVictoryEscape)
            {
                if (!hasStartedVictoryEscape)
                {
                    hasStartedVictoryEscape = true;
                    victoryEscapeView.PlayEscape();
                }

                if (!hasCompletedVictoryEscape)
                {
                    view.Render(Hidden());
                    return;
                }
            }

            if (isVictory && !hasReportedLevelCleared)
            {
                hasReportedLevelCleared = true;
                reportLevelCleared?.Invoke(LevelStarRating.FromRemainingHealth(
                    healthSystem.CurrentHealth,
                    healthSystem.MaximumHealth));
            }

            if (!hasPlayedOutcomeSound)
            {
                soundPlayer?.Play(isVictory ? SoundId.LevelWon : SoundId.LevelLost);
                hasPlayedOutcomeSound = true;
            }

            view.Render(new LevelOutcomeHudState(
                true,
                isVictory ? LevelOutcome.Victory : LevelOutcome.Defeat,
                isVictory ? VictoryTitle : DefeatTitle,

                // A defeat scores nothing whatever the Cóc had left, and the rating already
                // says so for a run with none; asking it keeps the two in step even if a defeat
                // ever becomes possible with health to spare.
                isVictory
                    ? awardsFullStars
                        ? LevelStarRating.MaximumStars
                        : LevelStarRating.FromRemainingHealth(
                            healthSystem.CurrentHealth,
                            healthSystem.MaximumHealth)
                    : LevelStarRating.NoStars,
                healthSystem.CurrentHealth,
                healthSystem.MaximumHealth,
                goldSystem.Balance,
                isVictory && hasNextLevel));
            levelMusicSystem?.SetOutcomeVisible(true);
        }

        private static LevelOutcomeHudState Hidden()
        {
            return new LevelOutcomeHudState(
                false, LevelOutcome.Victory, string.Empty, 0, 0, 0, 0, false);
        }

        private void HandlePlayAgainRequested()
        {
            soundPlayer?.Play(SoundId.ButtonPressed);
            requestReplayLevel?.Invoke();
        }

        private void HandleNextLevelRequested()
        {
            // Sounded before the guard, not inside it: the button is on screen and was pressed,
            // so a silent press would read as a dead button rather than as a refusal.
            soundPlayer?.Play(SoundId.ButtonPressed);
            if (hasNextLevel && waveSystem.CreateState().Phase == WavePhase.Victory)
            {
                requestNextLevel?.Invoke();
            }
        }

        private void HandleReturnToLevelMenuRequested()
        {
            soundPlayer?.Play(SoundId.ButtonPressed);
            requestReturnToLevelMenu?.Invoke();
        }

        private void HandleVictoryEscapeCompleted()
        {
            hasCompletedVictoryEscape = true;
            Refresh();
        }
    }
}
