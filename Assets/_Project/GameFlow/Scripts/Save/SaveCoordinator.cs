using System;
using UnityEngine;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Owns unlocked-level runtime state and coordinates snapshot-based persistence.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SaveCoordinator : MonoBehaviour
    {
        private LocalSaveRepository repository;

        public UnlockProgress Progress { get; private set; }
        public SaveWriteResult LastWriteResult { get; private set; }
        public bool HasProgress => Progress != null;

        public SaveLoadResult Initialize()
        {
            EnsureRepository();

            SaveLoadResult loadResult = repository.Load();
            if (loadResult.IsSuccess)
            {
                Progress = new UnlockProgress(loadResult.Data.UnlockedLevelNumbers);
                LastWriteResult = new SaveWriteResult(SaveWriteStatus.Success, string.Empty);
                return loadResult;
            }

            if (loadResult.Status == SaveLoadStatus.Missing)
            {
                Progress = new UnlockProgress();
                LastWriteResult = SaveCurrent();
                return loadResult;
            }

            Progress = null;
            LastWriteResult = new SaveWriteResult(SaveWriteStatus.Success, string.Empty);
            return loadResult;
        }

        public UnlockAttemptResult TryUnlockAndSave(int levelNumber, out SaveWriteResult writeResult)
        {
            if (Progress == null)
            {
                writeResult = new SaveWriteResult(
                    SaveWriteStatus.ValidationFailed,
                    "Unlock progress is not initialized.");
                return UnlockAttemptResult.InvalidLevel;
            }

            UnlockAttemptResult unlockResult = Progress.TryUnlock(levelNumber);
            writeResult = unlockResult == UnlockAttemptResult.Unlocked
                ? SaveCurrent()
                : new SaveWriteResult(SaveWriteStatus.Success, string.Empty);
            return unlockResult;
        }

        public SaveWriteResult RetrySave()
        {
            return SaveCurrent();
        }

        public SaveWriteResult StartNew()
        {
            EnsureRepository();
            SaveWriteResult deleteResult = repository.DeleteOwnedAutosave();
            if (!deleteResult.IsSuccess)
            {
                LastWriteResult = deleteResult;
                return deleteResult;
            }

            Progress = new UnlockProgress();
            LastWriteResult = SaveCurrent();
            return LastWriteResult;
        }

        private SaveWriteResult SaveCurrent()
        {
            EnsureRepository();
            if (Progress == null)
            {
                LastWriteResult = new SaveWriteResult(
                    SaveWriteStatus.ValidationFailed,
                    "Unlock progress is not initialized.");
                return LastWriteResult;
            }

            SaveRootV1 snapshot = SaveRootV1.Create(
                Progress.CreateSortedSnapshot(),
                DateTime.UtcNow.ToString("O"),
                Application.version);
            LastWriteResult = repository.Save(snapshot);
            return LastWriteResult;
        }

        private void EnsureRepository()
        {
            if (repository == null)
            {
                repository = new LocalSaveRepository(Application.persistentDataPath);
            }
        }
    }
}
