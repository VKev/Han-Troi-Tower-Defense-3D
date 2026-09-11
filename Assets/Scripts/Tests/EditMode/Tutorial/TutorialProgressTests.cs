using NUnit.Framework;
using TowerDefense3D.GameFlow;
using TowerDefense3D.Tutorials;

namespace TowerDefense3D.GameFlow.Tests.EditMode
{
    public sealed class TutorialProgressTests
    {
        [Test]
        public void SessionSteps_DoNotPersistUntilLevelOneTutorialCompletes()
        {
            var progress = new TutorialProgress();

            progress.MarkCompleted("first_link_v2");
            progress.MarkCompleted("second_wave_expansion_v1");

            Assert.That(progress.IsCompleted("first_link_v2"), Is.True);
            Assert.That(progress.CreateSnapshot(), Is.Empty);

            progress.ResetLevelOneSession();

            Assert.That(progress.IsCompleted("first_link_v2"), Is.False);
            Assert.That(progress.IsCompleted("second_wave_expansion_v1"), Is.False);
        }

        [Test]
        public void CompletingLevelOneTutorial_PersistsAndSkipsEveryLevelOneTutorial()
        {
            var progress = new TutorialProgress();

            progress.CompleteLevelOneTutorial();

            Assert.That(progress.HasCompletedLevelOneTutorial, Is.True);
            Assert.That(progress.IsCompleted("first_link_v2"), Is.True);
            Assert.That(progress.IsCompleted("second_wave_expansion_v1"), Is.True);
            Assert.That(progress.IsCompleted("fire_tower_v1"), Is.True);

            var restored = new TutorialProgress();
            restored.Restore(progress.CreateSnapshot());

            Assert.That(restored.HasCompletedLevelOneTutorial, Is.True);
            Assert.That(restored.IsCompleted("fire_tower_v1"), Is.True);
        }

        [Test]
        public void LegacyPartialTutorialRecords_DoNotSkipTheNewLevelOneTutorial()
        {
            var progress = new TutorialProgress();

            progress.Restore(new[] { new TutorialSaveRecord("first_link_v2") });

            Assert.That(progress.HasCompletedLevelOneTutorial, Is.False);
            Assert.That(progress.IsCompleted("first_link_v2"), Is.False);
        }

        [Test]
        public void CompletingLevelOneTutorial_WritesThePermanentUnlockToSave()
        {
            var repository = new SaveRepositoryStub();
            var progress = new TutorialProgress();
            var saveSystem = new SaveSystem(repository, "test", progress);
            saveSystem.Initialize();

            progress.MarkCompleted("first_link_v2");
            Assert.That(repository.Snapshot.Tutorials, Is.Empty);

            progress.CompleteLevelOneTutorial();

            Assert.That(repository.Snapshot.Tutorials, Has.Length.EqualTo(1));
            Assert.That(
                repository.Snapshot.Tutorials[0].TutorialId,
                Is.EqualTo(TutorialProgress.LevelOneTutorialCompleteId));

            var loadedProgress = new TutorialProgress();
            var loadedSaveSystem = new SaveSystem(repository, "test", loadedProgress);
            loadedSaveSystem.Initialize();

            Assert.That(loadedProgress.HasCompletedLevelOneTutorial, Is.True);
        }

        private sealed class SaveRepositoryStub : ISaveRepository
        {
            public SaveSnapshot Snapshot { get; private set; }

            public SaveLoadResult Load()
            {
                return Snapshot == null
                    ? new SaveLoadResult(SaveLoadStatus.Missing, null, string.Empty)
                    : new SaveLoadResult(SaveLoadStatus.Success, Snapshot, string.Empty);
            }

            public SaveWriteResult Save(SaveSnapshot snapshot)
            {
                Snapshot = snapshot;
                return new SaveWriteResult(SaveWriteStatus.Success, string.Empty);
            }

            public SaveWriteResult DeleteOwnedAutosave()
            {
                Snapshot = null;
                return new SaveWriteResult(SaveWriteStatus.Success, string.Empty);
            }
        }
    }
}
