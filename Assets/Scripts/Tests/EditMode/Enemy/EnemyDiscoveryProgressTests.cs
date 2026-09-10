using NUnit.Framework;
using TowerDefense3D.Enemies;
using TowerDefense3D.GameFlow;

namespace TowerDefense3D.GameFlow.Tests.EditMode
{
    public sealed class EnemyDiscoveryProgressTests
    {
        [Test]
        public void MarkDiscovered_PersistsTheStableEnemyId()
        {
            var progress = new EnemyDiscoveryProgress();

            progress.MarkDiscovered("stealth");

            Assert.That(progress.IsDiscovered("stealth"), Is.True);
            Assert.That(progress.CreateSnapshot(), Is.EqualTo(new[] { "stealth" }));
        }

        [Test]
        public void Restore_MissingEnemyDiscoverySectionMeansEveryEnemyIsNew()
        {
            var progress = new EnemyDiscoveryProgress();

            progress.Restore(null);

            Assert.That(progress.IsDiscovered("stealth"), Is.False);
        }

        [Test]
        public void MarkDiscovered_WritesToTheAutosaveSnapshot()
        {
            var repository = new SaveRepositoryStub();
            var progress = new EnemyDiscoveryProgress();
            var saveSystem = new SaveSystem(repository, "test", enemyDiscoveryProgress: progress);
            saveSystem.Initialize();

            progress.MarkDiscovered("stealth");

            Assert.That(repository.Snapshot.DiscoveredEnemyIds, Is.EqualTo(new[] { "stealth" }));
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
