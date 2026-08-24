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
        private readonly ApplicationUISystem applicationUiSystem;

        private GameFlowCoordinator coordinator;

        public LevelTransitionFlow(LevelSceneLoader levelSceneLoader, TowerNetworkManager towerNetworkManager,
            ApplicationUISystem applicationUiSystem)
        {
            this.levelSceneLoader = levelSceneLoader;
            this.towerNetworkManager = towerNetworkManager;
            this.applicationUiSystem = applicationUiSystem;
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
            applicationUiSystem.HideBlockingError();
            applicationUiSystem.HideLevelMenu();
            applicationUiSystem.ShowLoading($"Loading Level {request.LevelNumber}...");
            applicationUiSystem.SetInputBlocked(true);
            levelSceneLoader.LoadLevel(request, towerNetworkManager, coordinator.RequestReturnToLevelMenu,
                result => OnLevelLoadCompleted(request, result));
        }

        public void BeginReturnToLevelMenu()
        {
            coordinator.SetState(GameFlowState.LoadingLevel);
            applicationUiSystem.ShowLoading("Returning to Level Menu...");
            applicationUiSystem.SetInputBlocked(true);
            levelSceneLoader.UnloadActiveLevel(OnReturnToMenuCompleted);
        }

        private void OnLevelLoadCompleted(LevelLoadRequest request, LevelTransitionResult result)
        {
            if (coordinator == null)
            {
                return;
            }

            applicationUiSystem.HideLoading();
            if (result.IsSuccess)
            {
                coordinator.SetState(GameFlowState.Gameplay);
                applicationUiSystem.SetInputBlocked(false);
                return;
            }

            coordinator.SetState(GameFlowState.BlockingError);
            applicationUiSystem.SetInputBlocked(false);
            applicationUiSystem.ShowBlockingError(CreateTransitionErrorMessage(result),
                () => BeginLevelLoad(request), null);
        }

        private void OnReturnToMenuCompleted(LevelTransitionResult result)
        {
            if (coordinator == null)
            {
                return;
            }

            applicationUiSystem.HideLoading();
            if (result.IsSuccess)
            {
                coordinator.ShowLevelMenu();
                return;
            }

            coordinator.SetState(GameFlowState.BlockingError);
            applicationUiSystem.SetInputBlocked(false);
            applicationUiSystem.ShowBlockingError(CreateTransitionErrorMessage(result),
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
