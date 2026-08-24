using System.Collections.Generic;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Builds level-menu state and owns level-selection callbacks.
    /// </summary>
    public sealed class LevelMenuFlow
    {
        private readonly LevelCatalog levelCatalog;
        private readonly SaveSystem saveSystem;
        private readonly ApplicationUISystem applicationUiSystem;

        private GameFlowCoordinator coordinator;

        public LevelMenuFlow(LevelCatalog levelCatalog, SaveSystem saveSystem,
            ApplicationUISystem applicationUiSystem)
        {
            this.levelCatalog = levelCatalog;
            this.saveSystem = saveSystem;
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

        public void Show()
        {
            List<LevelCatalogEntry> orderedLevels = levelCatalog.CreateOrderedSnapshot();
            var items = new List<LevelMenuItemState>(orderedLevels.Count);
            for (int index = 0; index < orderedLevels.Count; index++)
            {
                LevelCatalogEntry entry = orderedLevels[index];
                bool isUnlocked = saveSystem.Progress.IsUnlocked(entry.LevelNumber);
                items.Add(new LevelMenuItemState(entry.LevelNumber, entry.DisplayName, isUnlocked, false));
            }

            coordinator.SetState(GameFlowState.LevelMenu);
            applicationUiSystem.HideLoading();
            applicationUiSystem.HideBlockingError();
            applicationUiSystem.SetInputBlocked(false);
            applicationUiSystem.ShowLevelMenu(items, HandleLevelSelected);
        }

        private void HandleLevelSelected(int levelNumber)
        {
            if (!levelCatalog.TryGetLevel(levelNumber, out LevelCatalogEntry entry))
            {
                return;
            }

            if (!saveSystem.Progress.IsUnlocked(levelNumber))
            {
                saveSystem.TryUnlockAndSave(levelNumber, out SaveWriteResult writeResult);
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
