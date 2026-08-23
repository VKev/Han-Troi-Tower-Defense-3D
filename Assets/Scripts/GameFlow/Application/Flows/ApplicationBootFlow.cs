namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Validates boot data, loads progress, and owns boot-error recovery callbacks.
    /// </summary>
    public sealed class ApplicationBootFlow
    {
        private readonly LevelCatalog levelCatalog;
        private readonly SaveSystem saveSystem;
        private readonly IApplicationUIController applicationUi;

        private GameFlowCoordinator coordinator;

        public ApplicationBootFlow(LevelCatalog levelCatalog, SaveSystem saveSystem,
            IApplicationUIController applicationUi)
        {
            this.levelCatalog = levelCatalog;
            this.saveSystem = saveSystem;
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

        public void Boot()
        {
            coordinator.SetState(GameFlowState.Booting);
            applicationUi.HideBlockingError();
            applicationUi.HideLevelMenu();
            applicationUi.ShowLoading("Loading progress...");
            applicationUi.SetInputBlocked(true);

            if (!levelCatalog.TryValidate(out string catalogError))
            {
                ShowError($"Level Catalog is invalid: {catalogError}");
                return;
            }

            SaveLoadResult loadResult = saveSystem.Initialize();
            if (loadResult.IsSuccess || loadResult.Status == SaveLoadStatus.Missing)
            {
                coordinator.ShowLevelMenu();
                if (!saveSystem.LastWriteResult.IsSuccess)
                {
                    coordinator.ShowSaveWarning(saveSystem.LastWriteResult.Error);
                }

                return;
            }

            ShowError(loadResult.Error);
        }

        public void ShowError(string error)
        {
            coordinator.SetState(GameFlowState.BlockingError);
            applicationUi.HideLoading();
            applicationUi.SetInputBlocked(false);
            string message = string.IsNullOrWhiteSpace(error) ? "Progress could not be loaded." : error;
            applicationUi.ShowBlockingError(message, Boot, StartNewFromError);
        }

        private void StartNewFromError()
        {
            SaveWriteResult result = saveSystem.StartNew();
            if (!saveSystem.HasProgress)
            {
                ShowError(result.Error);
                return;
            }

            coordinator.ShowLevelMenu();
            if (!result.IsSuccess)
            {
                coordinator.ShowSaveWarning(result.Error);
            }
        }

    }
}
