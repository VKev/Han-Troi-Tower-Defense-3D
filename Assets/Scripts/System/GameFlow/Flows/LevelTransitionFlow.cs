namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Owns level load, return-to-menu, and transition-completion callbacks.
    /// </summary>
    public sealed class LevelTransitionFlow
    {
        private readonly LevelSceneSystem levelSceneSystem;
        private readonly ApplicationUISystem applicationUiSystem;

        private GameFlowSystem gameFlowSystem;

        public LevelTransitionFlow(
            LevelSceneSystem levelSceneSystem,
            ApplicationUISystem applicationUiSystem)
        {
            this.levelSceneSystem = levelSceneSystem;
            this.applicationUiSystem = applicationUiSystem;
        }

        public void Initialize(GameFlowSystem system)
        {
            gameFlowSystem = system;
        }

        public void Shutdown()
        {
            gameFlowSystem = null;
        }

        public void BeginLevelLoad(LevelLoadRequest request)
        {
            gameFlowSystem.SetState(GameFlowState.LoadingLevel);
            applicationUiSystem.HideBlockingError();
            applicationUiSystem.HideLevelMenu();
            applicationUiSystem.ShowLoading($"Loading Level {request.LevelNumber}...");
            applicationUiSystem.SetInputBlocked(true);
            levelSceneSystem.LoadLevel(request, result => OnLevelLoadCompleted(request, result));
        }

        public void BeginReturnToLevelMenu()
        {
            gameFlowSystem.SetState(GameFlowState.LoadingLevel);
            applicationUiSystem.ShowLoading("Returning to Level Menu...");
            applicationUiSystem.SetInputBlocked(true);
            levelSceneSystem.UnloadActiveLevel(OnReturnToMenuCompleted);
        }

        private void OnLevelLoadCompleted(LevelLoadRequest request, LevelTransitionResult result)
        {
            if (gameFlowSystem == null)
            {
                return;
            }

            applicationUiSystem.HideLoading();
            if (result.IsSuccess)
            {
                gameFlowSystem.SetState(GameFlowState.Gameplay);
                applicationUiSystem.SetInputBlocked(false);
                return;
            }

            gameFlowSystem.SetState(GameFlowState.BlockingError);
            applicationUiSystem.SetInputBlocked(false);
            applicationUiSystem.ShowBlockingError(CreateTransitionErrorMessage(result),
                () => BeginLevelLoad(request), null);
        }

        private void OnReturnToMenuCompleted(LevelTransitionResult result)
        {
            if (gameFlowSystem == null)
            {
                return;
            }

            applicationUiSystem.HideLoading();
            if (result.IsSuccess)
            {
                gameFlowSystem.ShowLevelMenu();
                return;
            }

            gameFlowSystem.SetState(GameFlowState.BlockingError);
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
