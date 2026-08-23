using System.Collections.Generic;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Builds level-menu state and owns level-selection callbacks.
    /// </summary>
    public sealed class LevelMenuFlow
    {
        private readonly LevelCatalog levelCatalog;
        private readonly SaveCoordinator saveCoordinator;
        private readonly IApplicationUIController applicationUi;

        private GameFlowCoordinator coordinator;

        public LevelMenuFlow(LevelCatalog levelCatalog, SaveCoordinator saveCoordinator,
            IApplicationUIController applicationUi)
        {
            this.levelCatalog = levelCatalog;
            this.saveCoordinator = saveCoordinator;
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

        public void Show()
        {
            List<LevelCatalogEntry> orderedLevels = levelCatalog.CreateOrderedSnapshot();
            var items = new List<LevelMenuItemState>(orderedLevels.Count);
            for (int index = 0; index < orderedLevels.Count; index++)
            {
                LevelCatalogEntry entry = orderedLevels[index];
                bool isUnlocked = saveCoordinator.Progress.IsUnlocked(entry.LevelNumber);
                items.Add(new LevelMenuItemState(entry.LevelNumber, entry.DisplayName, isUnlocked, false));
            }

            coordinator.SetState(GameFlowState.LevelMenu);
            applicationUi.HideLoading();
            applicationUi.HideBlockingError();
            applicationUi.SetInputBlocked(false);
            applicationUi.ShowLevelMenu(items, HandleLevelSelected);
        }

        private void HandleLevelSelected(int levelNumber)
        {
            if (!levelCatalog.TryGetLevel(levelNumber, out LevelCatalogEntry entry))
            {
                return;
            }

            if (!saveCoordinator.Progress.IsUnlocked(levelNumber))
            {
                saveCoordinator.TryUnlockAndSave(levelNumber, out SaveWriteResult writeResult);
                Show();
                if (!writeResult.IsSuccess)
                {
                    coordinator.ShowSaveWarning(writeResult.Error);
                }

                return;
            }

            coordinator.BeginLevelLoad(new LevelLoadRequest(entry.LevelNumber, entry.ScenePath));
        }

    }
}
