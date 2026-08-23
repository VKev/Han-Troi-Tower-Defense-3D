using System;
using TowerDefense3D.Towers;

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

        private bool isDirty;
        private bool isStarted;

        public GameplayUISystem(
            IGameplayUIView gameplayView,
            IPlacementHudView placementHudView,
            TowerNetworkSystem towerNetworkSystem,
            TowerNetworkHudPresenter towerNetworkHudPresenter)
        {
            this.gameplayView = gameplayView ?? throw new ArgumentNullException(nameof(gameplayView));
            this.placementHudView = placementHudView ?? throw new ArgumentNullException(nameof(placementHudView));
            this.towerNetworkSystem = towerNetworkSystem
                ?? throw new ArgumentNullException(nameof(towerNetworkSystem));
            this.towerNetworkHudPresenter = towerNetworkHudPresenter
                ?? throw new ArgumentNullException(nameof(towerNetworkHudPresenter));
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
            towerNetworkSystem.StateChanged += MarkDirty;
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
        }

        public void Dispose()
        {
            if (!isStarted)
            {
                return;
            }

            isStarted = false;
            towerNetworkSystem.StateChanged -= MarkDirty;
            towerNetworkHudPresenter.Disconnect();
        }

        private void MarkDirty()
        {
            isDirty = true;
        }
    }
}
