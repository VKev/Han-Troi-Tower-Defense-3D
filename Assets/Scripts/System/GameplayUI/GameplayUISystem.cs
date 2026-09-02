using System;
using TowerDefense3D.Economy;
using TowerDefense3D.Enemies;
using TowerDefense3D.Simulation;
using TowerDefense3D.Towers;
using TowerDefense3D.Waves;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Owns level HUD lifecycle and refreshes presentation only after tower state changes.
    /// </summary>
    public sealed class GameplayUISystem : IDisposable
    {
        private readonly IGameplayUIView gameplayView;
        private readonly IPlacementHudView placementHudView;
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
        private readonly LevelSkipCheatPresenter skipCheatPresenter;
        private readonly LevelOutcomeHudPresenter levelOutcomeHudPresenter;

        private bool isDirty;
        private bool isStarted;

        public GameplayUISystem(
            IGameplayUIView gameplayView,
            IPlacementHudView placementHudView,
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
            this.placementHudView = placementHudView ?? throw new ArgumentNullException(nameof(placementHudView));
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
            IPlacementHudView placementHudView,
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
            LevelOutcomeHudPresenter levelOutcomeHudPresenter)
            : this(
                gameplayView,
                placementHudView,
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
            this.skipCheatPresenter = skipCheatPresenter
                ?? throw new ArgumentNullException(nameof(skipCheatPresenter));
            this.levelOutcomeHudPresenter = levelOutcomeHudPresenter
                ?? throw new ArgumentNullException(nameof(levelOutcomeHudPresenter));
        }

        public void BindReturnToMenu(Action requestReturnToMenu)
        {
            towerNetworkHudPresenter.BindReturnToMenu(requestReturnToMenu);
        }

        public void Start()
        {
            gameplayView.Show();
            placementHudView.Show();
            towerNetworkHudPresenter.Connect();
            waveHudPresenter.Connect();
            towerNetworkSystem.StateChanged += MarkDirty;
            waveSystem.StateChanged += MarkDirty;
            enemySystem.EnemySpawned += MarkDirty;
            enemySystem.EnemyKilled += MarkDirty;
            enemySystem.EnemyLeaked += MarkDirty;
            goldSystem.BalanceChanged += HandleGoldChanged;
            healthSystem.HealthChanged += HandleHealthChanged;
            statusHudView.RenderGold(goldSystem.Balance);
            statusHudView.RenderHealth(healthSystem.CurrentHealth, healthSystem.MaximumHealth);
            if (simulationSystem != null && pauseHudView != null)
            {
                pauseHudView.Initialize();
                pauseHudView.Render(simulationSystem.IsPaused);
                pauseHudView.Show();
                pauseHudView.PauseToggleRequested += HandlePauseToggleRequested;
            }

            skipCheatPresenter?.Connect();
            levelOutcomeHudPresenter?.Connect();
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
            towerNetworkHudPresenter.Refresh();
            waveHudPresenter.Refresh();
            skipCheatPresenter?.Refresh();
            levelOutcomeHudPresenter?.Refresh();
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
            if (simulationSystem != null && pauseHudView != null)
            {
                pauseHudView.PauseToggleRequested -= HandlePauseToggleRequested;
                pauseHudView.Shutdown();
            }

            skipCheatPresenter?.Disconnect();
            levelOutcomeHudPresenter?.Disconnect();
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

            simulationSystem.SetPaused(!simulationSystem.IsPaused);
            pauseHudView.Render(simulationSystem.IsPaused);
        }
    }
}
