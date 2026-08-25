using System;
using TowerDefense3D.Enemies;
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

        private bool isDirty;
        private bool isStarted;

        public GameplayUISystem(
            IGameplayUIView gameplayView,
            IPlacementHudView placementHudView,
            TowerNetworkSystem towerNetworkSystem,
            TowerNetworkHudPresenter towerNetworkHudPresenter,
            IWaveSystem waveSystem,
            WaveHudPresenter waveHudPresenter,
            EnemySystem enemySystem)
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
    }
}
