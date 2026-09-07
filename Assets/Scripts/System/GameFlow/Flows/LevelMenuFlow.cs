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
                bool isCleared = saveSystem.Progress.IsCleared(entry.LevelNumber);
                items.Add(new LevelMenuItemState(
                    entry.LevelNumber,
                    entry.DisplayName,
                    isUnlocked,
                    isCleared,
                    saveSystem.Progress.GetStars(entry.LevelNumber),
                    false));
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

        /// <summary>
        /// Persists one level as beaten at <paramref name="stars"/>. Replaying a level already
        /// cleared costs no save write unless the replay beat its recorded score.
        /// </summary>
        /// <remarks>
        /// The early-out used to be "already cleared", which was right while a clear was the
        /// only thing recorded. It is wrong now: a player replaying a beaten level to earn its
        /// third star would have had that run thrown away. Whether there is anything new to
        /// write is a question only the progress can answer, so it is left to answer it.
        /// </remarks>
        public void MarkLevelCleared(int levelNumber, int stars)
        {
            saveSystem.TryMarkClearedAndSave(levelNumber, stars, out SaveWriteResult writeResult);
            if (!writeResult.IsSuccess)
            {
                gameFlowSystem.ShowSaveWarning(writeResult.Error);
                return;
            }

            UnlockNextLevel(levelNumber);
        }

        /// <summary>
        /// Opens the level that follows the one just beaten.
        /// </summary>
        /// <remarks>
        /// Beating a level is what earns the next one, and until this ran nothing did that. The
        /// clear was recorded and the journey still showed the next level locked; it only ever
        /// opened because <see cref="PlayLevel"/> unlocks whatever it is asked to load. So going
        /// straight on from the victory panel worked while going back to the menu did not, which
        /// looked like the win had been thrown away.
        ///
        /// A level already open is left alone, so replaying an old level to improve its stars does
        /// not reopen anything or cost a write.
        /// </remarks>
        private void UnlockNextLevel(int levelNumber)
        {
            if (!levelCatalog.TryGetNextLevel(levelNumber, out LevelCatalogEntry next)
                || saveSystem.Progress.IsUnlocked(next.LevelNumber))
            {
                return;
            }

            saveSystem.TryUnlockAndSave(next.LevelNumber, out SaveWriteResult unlockResult);
            if (!unlockResult.IsSuccess)
            {
                gameFlowSystem.ShowSaveWarning(unlockResult.Error);
            }
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
