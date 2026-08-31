using System;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Owns unlocked-level runtime state and coordinates snapshot-based persistence.
    /// </summary>
    public sealed class SaveSystem
    {
        private readonly ISaveRepository repository;
        private readonly string applicationVersion;

        public SaveSystem(ISaveRepository repository, string applicationVersion)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.applicationVersion = applicationVersion;
        }

        public UnlockProgress Progress { get; private set; }
        public SaveWriteResult LastWriteResult { get; private set; }
        public bool HasProgress => Progress != null;

        public SaveLoadResult Initialize()
        {
            SaveLoadResult loadResult = repository.Load();
            if (loadResult.IsSuccess)
            {
                Progress = new UnlockProgress(
                    loadResult.Data.UnlockedLevelNumbers,
                    loadResult.Data.ClearedLevelNumbers);
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
            UnlockAttemptResult unlockResult = Progress.TryUnlock(levelNumber);
            writeResult = unlockResult == UnlockAttemptResult.Unlocked
                ? SaveCurrent()
                : new SaveWriteResult(SaveWriteStatus.Success, string.Empty);
            return unlockResult;
        }

        /// <summary>
        /// Records one level as beaten and persists it. Called once per victory, so a repeat
        /// clear of the same level costs no write.
        /// </summary>
        public UnlockAttemptResult TryMarkClearedAndSave(int levelNumber, out SaveWriteResult writeResult)
        {
            UnlockAttemptResult clearResult = Progress.TryMarkCleared(levelNumber);
            writeResult = clearResult == UnlockAttemptResult.Unlocked
                ? SaveCurrent()
                : new SaveWriteResult(SaveWriteStatus.Success, string.Empty);
            return clearResult;
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
            SaveSnapshot snapshot = SaveSnapshot.Create(
                Progress.CreateSortedSnapshot(),
                Progress.CreateSortedClearedSnapshot(),
                DateTime.UtcNow.ToString("O"),
                applicationVersion);
            LastWriteResult = repository.Save(snapshot);
            return LastWriteResult;
        }
    }
}
