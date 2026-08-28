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

        private GameFlowSystem gameFlowSystem;

        public LevelMenuFlow(LevelCatalog levelCatalog, SaveSystem saveSystem,
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

            gameFlowSystem.SetState(GameFlowState.LevelMenu);
            applicationUiSystem.HideLoading();
            applicationUiSystem.HideBlockingError();
            applicationUiSystem.SetInputBlocked(false);
            applicationUiSystem.ShowLevelMenu(items, HandleLevelSelected);
        }

        /// <summary>
        /// Loads one authored level directly, unlocking it first when progress has not reached it.
        /// </summary>
        public void PlayLevel(int levelNumber)
        {
            if (!levelCatalog.TryGetLevel(levelNumber, out LevelCatalogEntry entry))
            {
                return;
            }

            if (!saveSystem.Progress.IsUnlocked(entry.LevelNumber))
            {
                saveSystem.TryUnlockAndSave(entry.LevelNumber, out SaveWriteResult writeResult);
                if (!writeResult.IsSuccess)
                {
                    gameFlowSystem.ShowSaveWarning(writeResult.Error);
                }
            }

            gameFlowSystem.BeginLevelLoad(new LevelLoadRequest(entry.LevelNumber, entry.ScenePath));
        }

        public void PlayNextLevel(int currentLevelNumber)
        {
            if (levelCatalog.TryGetNextLevel(currentLevelNumber, out LevelCatalogEntry entry))
            {
                PlayLevel(entry.LevelNumber);
            }
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
                    gameFlowSystem.ShowSaveWarning(writeResult.Error);
                }

                return;
            }

            gameFlowSystem.BeginLevelLoad(new LevelLoadRequest(entry.LevelNumber, entry.ScenePath));
        }

    }
}
