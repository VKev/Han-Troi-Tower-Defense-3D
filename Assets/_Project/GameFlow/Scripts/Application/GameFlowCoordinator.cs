using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Sole owner of boot, menu, loading, gameplay, and blocking-error phases.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameFlowCoordinator : MonoBehaviour
    {
        [SerializeField] private LevelCatalog levelCatalog;
        [SerializeField] private SaveCoordinator saveCoordinator;
        [SerializeField] private LevelSceneLoader levelSceneLoader;
        [SerializeField] private MonoBehaviour applicationUiBehaviour;

        private IApplicationUI applicationUi;

        public GameFlowState State { get; private set; } = GameFlowState.Booting;

        private void Awake()
        {
            applicationUi = applicationUiBehaviour as IApplicationUI;
            if (levelCatalog == null
                || saveCoordinator == null
                || levelSceneLoader == null
                || applicationUi == null)
            {
                Debug.LogError(
                    "GameFlowCoordinator requires a LevelCatalog, SaveCoordinator, LevelSceneLoader, and IApplicationUI.",
                    this);
                enabled = false;
            }
        }

        private void Start()
        {
            BootNow();
        }

        public void BootNow()
        {
            if (!enabled || applicationUi == null)
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
            if (State != GameFlowState.Gameplay)
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
                RequestReturnToLevelMenu,
                result => OnLevelLoadCompleted(request, result));
        }

        private void OnLevelLoadCompleted(LevelLoadRequest request, LevelTransitionResult result)
        {
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
