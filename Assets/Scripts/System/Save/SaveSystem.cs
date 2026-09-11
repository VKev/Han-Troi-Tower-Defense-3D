using System;
using TowerDefense3D.Enemies;
using TowerDefense3D.Tutorials;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Owns unlocked-level runtime state and coordinates snapshot-based persistence.
    /// </summary>
    public sealed class SaveSystem
    {
        private readonly ISaveRepository repository;
        private readonly string applicationVersion;
        private readonly TutorialProgress tutorialProgress;
        private readonly EnemyDiscoveryProgress enemyDiscoveryProgress;

        public SaveSystem(
            ISaveRepository repository,
            string applicationVersion,
            TutorialProgress tutorialProgress = null,
            EnemyDiscoveryProgress enemyDiscoveryProgress = null)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.applicationVersion = applicationVersion;
            this.tutorialProgress = tutorialProgress;
            this.enemyDiscoveryProgress = enemyDiscoveryProgress;
            if (tutorialProgress != null)
            {
                tutorialProgress.PermanentProgressChanged += SaveTutorialProgress;
            }
            if (enemyDiscoveryProgress != null)
            {
                enemyDiscoveryProgress.Changed += SaveTutorialProgress;
            }
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
                    loadResult.Data.ClearedLevelNumbers,
                    loadResult.Data.LevelStars);
                tutorialProgress?.Restore(loadResult.Data.Tutorials);
                enemyDiscoveryProgress?.Restore(loadResult.Data.DiscoveredEnemyIds);
                LastWriteResult = new SaveWriteResult(SaveWriteStatus.Success, string.Empty);
                return loadResult;
            }

            if (loadResult.Status == SaveLoadStatus.Missing)
            {
                Progress = new UnlockProgress();
                tutorialProgress?.Restore(null);
                enemyDiscoveryProgress?.Restore(null);
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
        /// Records one level as beaten at <paramref name="stars"/> and persists it. A repeat
        /// clear costs a write only when it beat the score already on record.
        /// </summary>
        public UnlockAttemptResult TryMarkClearedAndSave(
            int levelNumber,
            int stars,
            out SaveWriteResult writeResult)
        {
            UnlockAttemptResult clearResult = Progress.TryMarkCleared(levelNumber, stars);
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
            tutorialProgress?.Restore(null);
            enemyDiscoveryProgress?.Restore(null);
            LastWriteResult = SaveCurrent();
            return LastWriteResult;
        }

        private void SaveTutorialProgress()
        {
            if (Progress != null)
            {
                SaveCurrent();
            }
        }

        private SaveWriteResult SaveCurrent()
        {
            SaveSnapshot snapshot = SaveSnapshot.Create(
                Progress.CreateSortedSnapshot(),
                Progress.CreateSortedClearedSnapshot(),
                Progress.CreateSortedStarSnapshot(),
                tutorialProgress?.CreateSnapshot() ?? Array.Empty<TutorialSaveRecord>(),
                enemyDiscoveryProgress?.CreateSnapshot() ?? Array.Empty<string>(),
                DateTime.UtcNow.ToString("O"),
                applicationVersion);
            LastWriteResult = repository.Save(snapshot);
            return LastWriteResult;
        }
    }
}
