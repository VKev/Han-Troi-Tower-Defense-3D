using TowerDefense3D.Towers;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Owns level load, return-to-menu, and transition-completion callbacks.
    /// </summary>
    public sealed class LevelTransitionFlow
    {
        private readonly LevelSceneLoader levelSceneLoader;
        private readonly TowerNetworkManager towerNetworkManager;
        private readonly IApplicationUIController applicationUi;

        private GameFlowCoordinator coordinator;

        public LevelTransitionFlow(LevelSceneLoader levelSceneLoader, TowerNetworkManager towerNetworkManager,
            IApplicationUIController applicationUi)
        {
            this.levelSceneLoader = levelSceneLoader;
            this.towerNetworkManager = towerNetworkManager;
            this.applicationUi = applicationUi;
        }

        public void Initialize(GameFlowCoordinator coordinator)
        {
            this.coordinator = coordinator;
        }

        public void Shutdown()
        {
            coordinator = null;
        }

        public void BeginLevelLoad(LevelLoadRequest request)
        {
            coordinator.SetState(GameFlowState.LoadingLevel);
            applicationUi.HideBlockingError();
            applicationUi.HideLevelMenu();
            applicationUi.ShowLoading($"Loading Level {request.LevelNumber}...");
            applicationUi.SetInputBlocked(true);
            levelSceneLoader.LoadLevel(request, towerNetworkManager, coordinator.RequestReturnToLevelMenu,
                result => OnLevelLoadCompleted(request, result));
        }

        public void BeginReturnToLevelMenu()
        {
            coordinator.SetState(GameFlowState.LoadingLevel);
            applicationUi.ShowLoading("Returning to Level Menu...");
            applicationUi.SetInputBlocked(true);
            levelSceneLoader.UnloadActiveLevel(OnReturnToMenuCompleted);
        }

        private void OnLevelLoadCompleted(LevelLoadRequest request, LevelTransitionResult result)
        {
            if (coordinator == null)
            {
                return;
            }

            applicationUi.HideLoading();
            if (result.IsSuccess)
            {
                coordinator.SetState(GameFlowState.Gameplay);
                applicationUi.SetInputBlocked(false);
                return;
            }

            coordinator.SetState(GameFlowState.BlockingError);
            applicationUi.SetInputBlocked(false);
            applicationUi.ShowBlockingError(CreateTransitionErrorMessage(result),
                () => BeginLevelLoad(request), null);
        }

        private void OnReturnToMenuCompleted(LevelTransitionResult result)
        {
            if (coordinator == null)
            {
                return;
            }

            applicationUi.HideLoading();
            if (result.IsSuccess)
            {
                coordinator.ShowLevelMenu();
                return;
            }

            coordinator.SetState(GameFlowState.BlockingError);
            applicationUi.SetInputBlocked(false);
            applicationUi.ShowBlockingError(CreateTransitionErrorMessage(result),
                BeginReturnToLevelMenu, null);
        }

        private static string CreateTransitionErrorMessage(LevelTransitionResult result)
        {
            return string.IsNullOrWhiteSpace(result.Error)
                ? $"Level transition failed with status {result.Status}."
                : result.Error;
        }
    }
}
