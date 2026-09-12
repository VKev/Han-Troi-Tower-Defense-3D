using System;
using System.Collections.Generic;
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
                    loadResult.Data.LevelStars,
                    loadResult.Data.Gold,
                    loadResult.Data.UnlockedTowerIds);
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
            return TryMarkClearedAndSave(levelNumber, stars, 0, out writeResult);
        }

        /// <summary>
        /// Records the clear and pays <paramref name="goldAward"/> into the wallet in one write.
        /// </summary>
        /// <remarks>
        /// The two travel together rather than as two calls because they are one event: a run
        /// that earned a star earned the gold behind it, and a write that landed one without the
        /// other would leave a save that contradicts itself. The award is asked whether it
        /// changed anything in its own right, so a payout can force a write even in the case -
        /// which should not arise - where the score on record did not move.
        /// </remarks>
        public UnlockAttemptResult TryMarkClearedAndSave(
            int levelNumber,
            int stars,
            int goldAward,
            out SaveWriteResult writeResult)
        {
            UnlockAttemptResult clearResult = Progress.TryMarkCleared(levelNumber, stars);
            bool paid = Progress.TryAddGold(goldAward);
            writeResult = clearResult == UnlockAttemptResult.Unlocked || paid
                ? SaveCurrent()
                : new SaveWriteResult(SaveWriteStatus.Success, string.Empty);
            return clearResult;
        }

        /// <summary>
        /// Records towers as earned and persists them, at the moment the player earns them.
        /// </summary>
        /// <remarks>
        /// Takes the whole batch rather than one tower at a time, and writes once for all of
        /// them: entering a level can earn several at once - four of them on the first level
        /// past the tutorial - and that is one event, not four.
        ///
        /// Called from the level HUD every time it refreshes, so the no-change path has to cost
        /// nothing: towers already on record add nothing to the set and write nothing.
        /// </remarks>
        public bool TryUnlockTowersAndSave(
            IReadOnlyList<string> towerIds,
            out SaveWriteResult writeResult)
        {
            writeResult = new SaveWriteResult(SaveWriteStatus.Success, string.Empty);
            if (Progress == null || towerIds == null)
            {
                return false;
            }

            bool changed = false;
            for (int index = 0; index < towerIds.Count; index++)
            {
                changed |= Progress.TryUnlockTower(towerIds[index]);
            }

            if (!changed)
            {
                return false;
            }

            writeResult = SaveCurrent();
            return true;
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
                Progress.Gold,
                Progress.CreateSortedTowerSnapshot(),
                DateTime.UtcNow.ToString("O"),
                applicationVersion);
            LastWriteResult = repository.Save(snapshot);
            return LastWriteResult;
        }
    }
}
