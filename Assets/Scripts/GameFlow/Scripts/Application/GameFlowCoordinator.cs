using System;
using System.Collections.Generic;
using TowerDefense3D.Towers;
using VContainer.Unity;

namespace TowerDefense3D.GameFlow
{
    public interface IApplicationUIController : IApplicationUI
    {
        void Initialize();
        void Shutdown();
    }

    /// <summary>
    /// Sole owner of boot, menu, loading, gameplay, and blocking-error phases.
    /// </summary>
    public sealed class GameFlowCoordinator : IStartable, IDisposable
    {
        private readonly LevelCatalog levelCatalog;
        private readonly SaveCoordinator saveCoordinator;
        private readonly LevelSceneLoader levelSceneLoader;
        private readonly IApplicationUIController applicationUi;

        private readonly TowerNetworkManager towerNetworkManager;
        private bool isStarted;
        private bool isDisposed;

        public GameFlowState State { get; private set; } = GameFlowState.Booting;

        public GameFlowCoordinator(
            LevelCatalog levelCatalog,
            SaveCoordinator saveCoordinator,
            TowerNetworkManager towerNetworkManager,
            LevelSceneLoader levelSceneLoader,
            IApplicationUIController applicationUi)
        {
            this.levelCatalog = levelCatalog ?? throw new ArgumentNullException(nameof(levelCatalog));
            this.saveCoordinator = saveCoordinator ?? throw new ArgumentNullException(nameof(saveCoordinator));
            this.levelSceneLoader = levelSceneLoader ?? throw new ArgumentNullException(nameof(levelSceneLoader));
            this.applicationUi = applicationUi ?? throw new ArgumentNullException(nameof(applicationUi));
            this.towerNetworkManager =
                towerNetworkManager ?? throw new ArgumentNullException(nameof(towerNetworkManager));
        }

        public void Start()
        {
            if (isStarted || isDisposed)
            {
                return;
            }

            bool applicationUiInitialized = false;
            try
            {
                applicationUi.Initialize();
                applicationUiInitialized = true;
                isStarted = true;
                BootNow();
            }
            catch (Exception startupException)
            {
                isStarted = false;
                if (applicationUiInitialized)
                {
                    try
                    {
                        applicationUi.Shutdown();
                    }
                    catch (Exception rollbackException)
                    {
                        throw new AggregateException(
                            "GameFlowCoordinator startup and UI rollback both failed.",
                            startupException,
                            rollbackException);
                    }
                }

                throw;
            }
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            towerNetworkManager.EndLevelSession();
            if (!isStarted)
            {
                return;
            }

            isStarted = false;
            applicationUi.Shutdown();
        }

        public void BootNow()
        {
            if (!isStarted || isDisposed)
            {
                return;
            }

            SetState(GameFlowState.Booting);
            applicationUi.HideBlockingError();
            applicationUi.HideLevelMenu();
            applicationUi.ShowLoading("Loading progress...");
            applicationUi.SetInputBlocked(true);

            if (!levelCatalog.TryValidate(out string catalogError))
            {
                ShowBootError($"Level Catalog is invalid: {catalogError}");
                return;
            }

            SaveLoadResult loadResult = saveCoordinator.Initialize();
            if (loadResult.IsSuccess || loadResult.Status == SaveLoadStatus.Missing)
            {
                ShowLevelMenu();
                if (!saveCoordinator.LastWriteResult.IsSuccess)
                {
                    ShowSaveWarning(saveCoordinator.LastWriteResult.Error);
                }

                return;
            }

            ShowBootError(loadResult.Error);
        }

        public void RequestReturnToLevelMenu()
        {
            if (!isStarted || isDisposed || State != GameFlowState.Gameplay)
            {
                return;
            }

            BeginReturnToLevelMenu();
        }

        private void BeginReturnToLevelMenu()
        {
            SetState(GameFlowState.LoadingLevel);
            applicationUi.ShowLoading("Returning to Level Menu...");
            applicationUi.SetInputBlocked(true);
            levelSceneLoader.UnloadActiveLevel(OnReturnToMenuCompleted);
        }

        private void HandleLevelSelected(int levelNumber)
        {
            if (State != GameFlowState.LevelMenu || !levelCatalog.TryGetLevel(levelNumber, out LevelCatalogEntry entry))
            {
                return;
            }

            if (!saveCoordinator.Progress.IsUnlocked(levelNumber))
            {
                UnlockAttemptResult unlockResult = saveCoordinator.TryUnlockAndSave(
                    levelNumber,
                    out SaveWriteResult writeResult);
                if (unlockResult == UnlockAttemptResult.Unlocked)
                {
                    ShowLevelMenu();
                    if (!writeResult.IsSuccess)
                    {
                        ShowSaveWarning(writeResult.Error);
                    }
                }

                return;
            }

            BeginLevelLoad(new LevelLoadRequest(entry.LevelNumber, entry.ScenePath));
        }

        private void BeginLevelLoad(LevelLoadRequest request)
        {
            SetState(GameFlowState.LoadingLevel);
            applicationUi.HideBlockingError();
            applicationUi.HideLevelMenu();
            applicationUi.ShowLoading($"Loading Level {request.LevelNumber}...");
            applicationUi.SetInputBlocked(true);
            levelSceneLoader.LoadLevel(
                request,
                towerNetworkManager,
                RequestReturnToLevelMenu,
                result => OnLevelLoadCompleted(request, result));
        }

        private void OnLevelLoadCompleted(LevelLoadRequest request, LevelTransitionResult result)
        {
            if (!isStarted || isDisposed)
            {
                return;
            }

            applicationUi.HideLoading();
            if (result.IsSuccess)
            {
                SetState(GameFlowState.Gameplay);
                applicationUi.SetInputBlocked(false);
                return;
            }

            SetState(GameFlowState.BlockingError);
            applicationUi.SetInputBlocked(false);
            applicationUi.ShowBlockingError(
                CreateTransitionErrorMessage(result),
                () => BeginLevelLoad(request),
                null);
        }

        private void OnReturnToMenuCompleted(LevelTransitionResult result)
        {
            if (!isStarted || isDisposed)
            {
                return;
            }

            applicationUi.HideLoading();
            if (result.IsSuccess)
            {
                ShowLevelMenu();
                return;
            }

            SetState(GameFlowState.BlockingError);
            applicationUi.SetInputBlocked(false);
            applicationUi.ShowBlockingError(
                CreateTransitionErrorMessage(result),
                BeginReturnToLevelMenu,
                null);
        }

        private void ShowLevelMenu()
        {
            if (saveCoordinator.Progress == null)
            {
                ShowBootError("Unlock progress is unavailable.");
                return;
            }

            List<LevelCatalogEntry> orderedLevels = levelCatalog.CreateOrderedSnapshot();
            var items = new List<LevelMenuItemState>(orderedLevels.Count);
            for (int index = 0; index < orderedLevels.Count; index++)
            {
                LevelCatalogEntry entry = orderedLevels[index];
                items.Add(new LevelMenuItemState(
                    entry.LevelNumber,
                    entry.DisplayName,
                    saveCoordinator.Progress.IsUnlocked(entry.LevelNumber),
                    false));
            }

            SetState(GameFlowState.LevelMenu);
            applicationUi.HideLoading();
            applicationUi.HideBlockingError();
            applicationUi.SetInputBlocked(false);
            applicationUi.ShowLevelMenu(items, HandleLevelSelected);
        }

        private void ShowBootError(string error)
        {
            SetState(GameFlowState.BlockingError);
            applicationUi.HideLoading();
            applicationUi.SetInputBlocked(false);
            applicationUi.ShowBlockingError(
                string.IsNullOrWhiteSpace(error) ? "Progress could not be loaded." : error,
                BootNow,
                StartNewFromError);
        }

        private void StartNewFromError()
        {
            SaveWriteResult result = saveCoordinator.StartNew();
            if (!saveCoordinator.HasProgress)
            {
                ShowBootError(result.Error);
                return;
            }

            ShowLevelMenu();
            if (!result.IsSuccess)
            {
                ShowSaveWarning(result.Error);
            }
        }

        private void ShowSaveWarning(string error)
        {
            string message = string.IsNullOrWhiteSpace(error)
                ? "Progress is unlocked for this session but has not been saved."
                : error;
            applicationUi.ShowSaveWarning(message, RetrySave);
        }

        private void RetrySave()
        {
            SaveWriteResult result = saveCoordinator.RetrySave();
            if (result.IsSuccess)
            {
                applicationUi.HideSaveWarning();
            }
            else
            {
                ShowSaveWarning(result.Error);
            }
        }

        private void SetState(GameFlowState state)
        {
            State = state;
        }

        private static string CreateTransitionErrorMessage(LevelTransitionResult result)
        {
            return string.IsNullOrWhiteSpace(result.Error)
                ? $"Level transition failed with status {result.Status}."
                : result.Error;
        }
    }
}
