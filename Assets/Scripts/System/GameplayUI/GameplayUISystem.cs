using System;
using TowerDefense3D.Audio;
using TowerDefense3D.Economy;
using TowerDefense3D.Enemies;
using TowerDefense3D.Simulation;
using TowerDefense3D.Towers;
using TowerDefense3D.Tutorials;
using TowerDefense3D.Waves;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Owns level HUD lifecycle and refreshes presentation only after tower state changes.
    /// </summary>
    public sealed class GameplayUISystem : IDisposable
    {
        private readonly IGameplayUIView gameplayView;
        private readonly TowerNetworkSystem towerNetworkSystem;
        private readonly TowerNetworkHudPresenter towerNetworkHudPresenter;
        private readonly IWaveSystem waveSystem;
        private readonly WaveHudPresenter waveHudPresenter;
        private readonly EnemySystem enemySystem;
        private readonly LevelGoldSystem goldSystem;
        private readonly LevelBaseHealthSystem healthSystem;
        private readonly ILevelStatusHudView statusHudView;
        private readonly GameplaySimulationSystem simulationSystem;
        private readonly IPauseHudView pauseHudView;
        private readonly ITutorialOverlay tutorialOverlay;
        private readonly LevelSkipCheatPresenter skipCheatPresenter;
        private readonly LevelOutcomeHudPresenter levelOutcomeHudPresenter;
        private readonly WaveThreeDefeatHudPresenter waveThreeDefeatHudPresenter;
        private readonly PauseMenuHudPresenter pauseMenuHudPresenter;
        private readonly ISoundPlayer soundPlayer;

        private bool isDirty;
        private bool isStarted;

        public GameplayUISystem(
            IGameplayUIView gameplayView,
            TowerNetworkSystem towerNetworkSystem,
            TowerNetworkHudPresenter towerNetworkHudPresenter,
            IWaveSystem waveSystem,
            WaveHudPresenter waveHudPresenter,
            EnemySystem enemySystem,
            LevelGoldSystem goldSystem,
            LevelBaseHealthSystem healthSystem,
            ILevelStatusHudView statusHudView)
        {
            this.gameplayView = gameplayView ?? throw new ArgumentNullException(nameof(gameplayView));
            this.towerNetworkSystem = towerNetworkSystem
                ?? throw new ArgumentNullException(nameof(towerNetworkSystem));
            this.towerNetworkHudPresenter = towerNetworkHudPresenter
                ?? throw new ArgumentNullException(nameof(towerNetworkHudPresenter));
            this.waveSystem = waveSystem ?? throw new ArgumentNullException(nameof(waveSystem));
            this.waveHudPresenter = waveHudPresenter
                ?? throw new ArgumentNullException(nameof(waveHudPresenter));
            this.enemySystem = enemySystem ?? throw new ArgumentNullException(nameof(enemySystem));
            this.goldSystem = goldSystem ?? throw new ArgumentNullException(nameof(goldSystem));
            this.healthSystem = healthSystem ?? throw new ArgumentNullException(nameof(healthSystem));
            this.statusHudView = statusHudView ?? throw new ArgumentNullException(nameof(statusHudView));
        }

        public GameplayUISystem(
            IGameplayUIView gameplayView,
            TowerNetworkSystem towerNetworkSystem,
            TowerNetworkHudPresenter towerNetworkHudPresenter,
            IWaveSystem waveSystem,
            WaveHudPresenter waveHudPresenter,
            EnemySystem enemySystem,
            LevelGoldSystem goldSystem,
            LevelBaseHealthSystem healthSystem,
            ILevelStatusHudView statusHudView,
            GameplaySimulationSystem simulationSystem,
            IPauseHudView pauseHudView,
            LevelSkipCheatPresenter skipCheatPresenter,
            LevelOutcomeHudPresenter levelOutcomeHudPresenter,
            WaveThreeDefeatHudPresenter waveThreeDefeatHudPresenter,
            PauseMenuHudPresenter pauseMenuHudPresenter,
            ITutorialOverlay tutorialOverlay,
            ISoundPlayer soundPlayer = null)
            : this(
                gameplayView,
                towerNetworkSystem,
                towerNetworkHudPresenter,
                waveSystem,
                waveHudPresenter,
                enemySystem,
                goldSystem,
                healthSystem,
                statusHudView)
        {
            this.simulationSystem = simulationSystem
                ?? throw new ArgumentNullException(nameof(simulationSystem));
            this.pauseHudView = pauseHudView ?? throw new ArgumentNullException(nameof(pauseHudView));
            this.tutorialOverlay = tutorialOverlay ?? throw new ArgumentNullException(nameof(tutorialOverlay));
            this.skipCheatPresenter = skipCheatPresenter
                ?? throw new ArgumentNullException(nameof(skipCheatPresenter));
            this.levelOutcomeHudPresenter = levelOutcomeHudPresenter
                ?? throw new ArgumentNullException(nameof(levelOutcomeHudPresenter));
            this.waveThreeDefeatHudPresenter = waveThreeDefeatHudPresenter
                ?? throw new ArgumentNullException(nameof(waveThreeDefeatHudPresenter));
            this.pauseMenuHudPresenter = pauseMenuHudPresenter
                ?? throw new ArgumentNullException(nameof(pauseMenuHudPresenter));

            // Optional, unlike its neighbours: the edit-mode tests build this system without an
            // audio stack, and a missing player only costs the pause button its click.
            this.soundPlayer = soundPlayer;
        }

        public void BindReturnToMenu(Action requestReturnToMenu)
        {
            towerNetworkHudPresenter.BindReturnToMenu(requestReturnToMenu);
        }

        public void Start()
        {
            gameplayView.Show();
            towerNetworkHudPresenter.Connect();
            waveHudPresenter.Connect();
            towerNetworkSystem.StateChanged += MarkDirty;
            waveSystem.StateChanged += MarkDirty;
            enemySystem.EnemySpawned += MarkDirty;
            enemySystem.EnemyKilled += MarkDirty;
            enemySystem.EnemyLeaked += MarkDirty;
            goldSystem.BalanceChanged += HandleGoldChanged;
            healthSystem.HealthChanged += HandleHealthChanged;
            towerNetworkSystem.PurchaseRefusedForGold += HandlePurchaseRefusedForGold;
            statusHudView.RenderGold(goldSystem.Balance);
            statusHudView.RenderHealth(healthSystem.CurrentHealth, healthSystem.MaximumHealth);
            statusHudView.SetHealthVisible(true);
            if (simulationSystem != null && pauseHudView != null)
            {
                pauseHudView.Initialize();
                pauseHudView.Render(simulationSystem.IsPaused);
                pauseHudView.Show();
                pauseHudView.PauseToggleRequested += HandlePauseToggleRequested;
                pauseHudView.FastForwardToggleRequested += HandleFastForwardToggleRequested;
                pauseHudView.RenderFastForward(simulationSystem.IsFastForward);
                tutorialOverlay.BlackOverlayVisibilityChanged += HandleTutorialBlackOverlayVisibilityChanged;
                HandleTutorialBlackOverlayVisibilityChanged(tutorialOverlay.IsBlackOverlayVisible);
            }

            skipCheatPresenter?.Connect();
            levelOutcomeHudPresenter?.Connect();
            waveThreeDefeatHudPresenter?.Connect();
            pauseMenuHudPresenter?.Connect();
            if (pauseMenuHudPresenter != null)
            {
                pauseMenuHudPresenter.ResumeRequested += HandleResumeRequested;
            }
            isStarted = true;
            isDirty = true;
        }

        public void RefreshIfDirty()
        {
            if (!isDirty)
            {
                return;
            }

            isDirty = false;

            // Re-read rather than assumed: a tutorial beat can drop double speed on its own, and
            // the button would otherwise keep showing the state the player last chose.
            if (simulationSystem != null && pauseHudView != null)
            {
                pauseHudView.RenderFastForward(simulationSystem.IsFastForward);
            }

            towerNetworkHudPresenter.Refresh();
            waveHudPresenter.Refresh();
            skipCheatPresenter?.Refresh();
            levelOutcomeHudPresenter?.Refresh();
            waveThreeDefeatHudPresenter?.Refresh();
        }

        public void Dispose()
        {
            if (!isStarted)
            {
                return;
            }

            isStarted = false;
            towerNetworkSystem.StateChanged -= MarkDirty;
            waveSystem.StateChanged -= MarkDirty;
            enemySystem.EnemySpawned -= MarkDirty;
            enemySystem.EnemyKilled -= MarkDirty;
            enemySystem.EnemyLeaked -= MarkDirty;
            goldSystem.BalanceChanged -= HandleGoldChanged;
            healthSystem.HealthChanged -= HandleHealthChanged;
            towerNetworkSystem.PurchaseRefusedForGold -= HandlePurchaseRefusedForGold;
            if (simulationSystem != null && pauseHudView != null)
            {
                pauseHudView.PauseToggleRequested -= HandlePauseToggleRequested;
                pauseHudView.FastForwardToggleRequested -= HandleFastForwardToggleRequested;
                tutorialOverlay.BlackOverlayVisibilityChanged -= HandleTutorialBlackOverlayVisibilityChanged;
                pauseHudView.Shutdown();
            }

            skipCheatPresenter?.Disconnect();
            levelOutcomeHudPresenter?.Disconnect();
            waveThreeDefeatHudPresenter?.Disconnect();
            if (pauseMenuHudPresenter != null)
            {
                pauseMenuHudPresenter.ResumeRequested -= HandleResumeRequested;
            }

            pauseMenuHudPresenter?.Disconnect();
            towerNetworkHudPresenter.Disconnect();
            waveHudPresenter.Disconnect();
        }

        private void MarkDirty()
        {
            isDirty = true;
        }

        private void MarkDirty(EnemySnapshot _)
        {
            isDirty = true;
        }

        private void HandleGoldChanged(int balance)
        {
            statusHudView.RenderGold(balance);

            // The build cards dim by what the balance can reach, so the tower HUD has to be
            // rebuilt when it moves. Without this the dim only caught up on the next unrelated
            // change - typically not until a tower was placed or a wave ended.
            MarkDirty();
        }

        private void HandlePurchaseRefusedForGold()
        {
            statusHudView.PlayPurchaseRefusedFeedback();
        }

        private void HandleHealthChanged(int currentHealth, int maximumHealth)
        {
            statusHudView.RenderHealth(currentHealth, maximumHealth);
        }

        private void HandlePauseToggleRequested()
        {
            if (simulationSystem == null || pauseHudView == null)
            {
                return;
            }

            soundPlayer?.Play(SoundId.PausePressed);
            SetPaused(!simulationSystem.IsPaused);
        }

        private void HandleFastForwardToggleRequested()
        {
            if (simulationSystem == null || pauseHudView == null)
            {
                return;
            }

            soundPlayer?.Play(SoundId.ButtonPressed);
            simulationSystem.SetFastForward(!simulationSystem.IsFastForward);
            pauseHudView.RenderFastForward(simulationSystem.IsFastForward);
        }

        private void HandleTutorialBlackOverlayVisibilityChanged(bool visible)
        {
            if (pauseHudView == null) return;
            if (visible) pauseHudView.Hide();
            else pauseHudView.Show();
        }

        private void HandleResumeRequested()
        {
            if (simulationSystem == null || pauseHudView == null)
            {
                return;
            }

            SetPaused(false);
        }

        /// <summary>
        /// The one place the pause state changes, because three things have to move together:
        /// the simulation, the button's glyph, and the modal that follows the pause state.
        /// </summary>
        private void SetPaused(bool isPaused)
        {
            simulationSystem.SetPaused(isPaused);
            pauseHudView.Render(simulationSystem.IsPaused);
            pauseMenuHudPresenter?.Refresh();
        }
    }
}
