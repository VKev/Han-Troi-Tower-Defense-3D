using System;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Owns unlocked-level runtime state and coordinates snapshot-based persistence.
    /// </summary>
    public sealed class SaveCoordinator
    {
        private readonly LocalSaveRepository repository;
        private readonly string applicationVersion;

        public SaveCoordinator(LocalSaveRepository repository, string applicationVersion)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.applicationVersion = applicationVersion ?? string.Empty;
        }

        public UnlockProgress Progress { get; private set; }
        public SaveWriteResult LastWriteResult { get; private set; }
        public bool HasProgress => Progress != null;

        public SaveLoadResult Initialize()
        {
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
                applicationVersion);
            LastWriteResult = repository.Save(snapshot);
            return LastWriteResult;
        }
    }
}
