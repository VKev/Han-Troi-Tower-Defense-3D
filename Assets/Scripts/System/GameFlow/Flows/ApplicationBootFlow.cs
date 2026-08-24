namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Validates boot data, loads progress, and owns boot-error recovery callbacks.
    /// </summary>
    public sealed class ApplicationBootFlow
    {
        private readonly LevelCatalog levelCatalog;
        private readonly SaveSystem saveSystem;
        private readonly ApplicationUISystem applicationUiSystem;

        private GameFlowSystem gameFlowSystem;

        public ApplicationBootFlow(LevelCatalog levelCatalog, SaveSystem saveSystem,
            ApplicationUISystem applicationUiSystem)
        {
            this.levelCatalog = levelCatalog;
            this.saveSystem = saveSystem;
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

        public void Boot()
        {
            gameFlowSystem.SetState(GameFlowState.Booting);
            applicationUiSystem.HideBlockingError();
            applicationUiSystem.HideLevelMenu();
            applicationUiSystem.ShowLoading("Loading progress...");
            applicationUiSystem.SetInputBlocked(true);

            if (!levelCatalog.TryValidate(out string catalogError))
            {
                ShowError($"Level Catalog is invalid: {catalogError}");
                return;
            }

            SaveLoadResult loadResult = saveSystem.Initialize();
            if (loadResult.IsSuccess || loadResult.Status == SaveLoadStatus.Missing)
            {
                gameFlowSystem.ShowLevelMenu();
                if (!saveSystem.LastWriteResult.IsSuccess)
                {
                    gameFlowSystem.ShowSaveWarning(saveSystem.LastWriteResult.Error);
                }

                return;
            }

            ShowError(loadResult.Error);
        }

        private void ShowError(string error)
        {
            gameFlowSystem.SetState(GameFlowState.BlockingError);
            applicationUiSystem.HideLoading();
            applicationUiSystem.SetInputBlocked(false);
            string message = string.IsNullOrWhiteSpace(error) ? "Progress could not be loaded." : error;
            applicationUiSystem.ShowBlockingError(message, Boot, StartNewFromError);
        }

        private void StartNewFromError()
        {
            SaveWriteResult result = saveSystem.StartNew();
            if (!saveSystem.HasProgress)
            {
                ShowError(result.Error);
                return;
            }

            gameFlowSystem.ShowLevelMenu();
            if (!result.IsSuccess)
            {
                gameFlowSystem.ShowSaveWarning(result.Error);
            }
        }

    }
}
