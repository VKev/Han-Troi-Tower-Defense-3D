using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TowerDefense3D.GameFlow.Editor;
using TowerDefense3D.Towers;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.GameFlow.Tests.EditMode
{
    public sealed class GameFlowEditModeTests
    {
        private const string TowerCatalogPath =
            "Assets/Config/Towers/Catalogs/TowerCatalog.asset";

        private string testRoot;

        [SetUp]
        public void SetUp()
        {
            testRoot = Path.Combine(
                Path.GetTempPath(),
                "TowerDefense3D.GameFlow.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testRoot);
        }

        [TearDown]
        public void TearDown()
        {
            string allowedRoot = Path.GetFullPath(
                Path.Combine(Path.GetTempPath(), "TowerDefense3D.GameFlow.Tests"));
            string candidate = Path.GetFullPath(testRoot);
            if (candidate.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase)
                && Directory.Exists(candidate))
            {
                Directory.Delete(candidate, true);
            }
        }

        [Test]
        public void UnlockProgress_LevelOneStartsUnlocked_AndSelectedLevelUnlocksDirectly()
        {
            var progress = new UnlockProgress();

            Assert.That(progress.IsUnlocked(1), Is.True);
            Assert.That(progress.IsUnlocked(2), Is.False);
            Assert.That(progress.TryUnlock(2), Is.EqualTo(UnlockAttemptResult.Unlocked));
            Assert.That(progress.TryUnlock(2), Is.EqualTo(UnlockAttemptResult.AlreadyUnlocked));
            Assert.That(progress.IsUnlocked(2), Is.True);
            Assert.That(progress.IsUnlocked(3), Is.False, "Unlocking Level 2 must not unlock Level 3.");
            CollectionAssert.AreEqual(new[] { 1, 2 }, progress.CreateSortedSnapshot());
        }

        [Test]
        public void LevelCatalog_RejectsDuplicateNumberPathAndNonFullPath()
        {
            LevelCatalog catalog = ScriptableObject.CreateInstance<LevelCatalog>();
            try
            {
                SetCatalogEntries(
                    catalog,
                    new LevelCatalogEntry(1, "Level 1", "Assets/Scenes/Levels/Level_001.unity"),
                    new LevelCatalogEntry(1, "Duplicate", "Assets/Scenes/Levels/Level_002.unity"));
                Assert.That(catalog.TryValidate(out string duplicateNumber), Is.False);
                StringAssert.Contains("duplicated", duplicateNumber);

                SetCatalogEntries(
                    catalog,
                    new LevelCatalogEntry(1, "Level 1", "Assets/Scenes/Levels/Level_001.unity"),
                    new LevelCatalogEntry(2, "Level 2", "Assets/Scenes/Levels/Level_001.unity"));
                Assert.That(catalog.TryValidate(out string duplicatePath), Is.False);
                StringAssert.Contains("duplicated", duplicatePath);

                SetCatalogEntries(
                    catalog,
                    new LevelCatalogEntry(1, "Level 1", "Level_001"));
                Assert.That(catalog.TryValidate(out string nonFullPath), Is.False);
                StringAssert.Contains("full Assets", nonFullPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void AuthoredCatalogAndScenes_PassEditorValidation()
        {
            LevelCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(
                LevelCatalogValidator.DefaultCatalogPath);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(LevelCatalogValidator.CollectErrors(catalog), Is.Empty);
            Assert.That(catalog.TryGetLevel(1, out LevelCatalogEntry levelOne), Is.True);
            Assert.That(catalog.TryGetLevel(2, out LevelCatalogEntry levelTwo), Is.True);
            Assert.That(levelOne.ScenePath, Is.EqualTo("Assets/Scenes/Levels/Level_001.unity"));
            Assert.That(levelTwo.ScenePath, Is.EqualTo("Assets/Scenes/Levels/Level_002.unity"));
        }

        [Test]
        public void SaveRepository_MissingThenRoundTrip_IsDeterministic()
        {
            var repository = new LocalSaveRepository(testRoot);
            Assert.That(repository.Load().Status, Is.EqualTo(SaveLoadStatus.Missing));

            SaveWriteResult write = repository.Save(
                SaveRootV1.Create(new[] { 2, 1 }, "2026-08-15T00:00:00.0000000Z", "test"));
            SaveLoadResult load = repository.Load();

            Assert.That(write.IsSuccess, Is.True, write.Error);
            Assert.That(load.IsSuccess, Is.True, load.Error);
            CollectionAssert.AreEqual(new[] { 2, 1 }, load.Data.UnlockedLevelNumbers);
            Assert.That(load.Data.SlotId, Is.EqualTo(SaveRootV1.AutosaveSlotId));
        }

        [Test]
        public void SaveRepository_CorruptPrimary_RecoversValidBackup()
        {
            var repository = new LocalSaveRepository(testRoot);
            Assert.That(
                repository.Save(SaveRootV1.Create(new[] { 1 }, "first", "test")).IsSuccess,
                Is.True);
            Assert.That(
                repository.Save(SaveRootV1.Create(new[] { 1, 2 }, "second", "test")).IsSuccess,
                Is.True);

            File.WriteAllText(repository.PrimaryPath, "{not-json");
            SaveLoadResult recovered = repository.Load();

            Assert.That(recovered.IsSuccess, Is.True, recovered.Error);
            CollectionAssert.AreEqual(new[] { 1 }, recovered.Data.UnlockedLevelNumbers);
        }

        [Test]
        public void SaveRepository_CorruptPrimaryAndBackup_ReturnsCorrupt()
        {
            var repository = new LocalSaveRepository(testRoot);
            Directory.CreateDirectory(repository.SaveRoot);
            File.WriteAllText(repository.PrimaryPath, "bad primary");
            File.WriteAllText(repository.BackupPath, "bad backup");

            SaveLoadResult result = repository.Load();

            Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.Corrupt));
            Assert.That(result.Data, Is.Null);
        }

        [Test]
        public void SaveRepository_UnknownSchema_IsNotOverwritten()
        {
            var repository = new LocalSaveRepository(testRoot);
            Directory.CreateDirectory(repository.SaveRoot);
            const string incompatible =
                "{\"schemaVersion\":99,\"slotId\":\"autosave\",\"savedAtUtc\":\"now\","
                + "\"appVersion\":\"test\",\"unlockedLevelNumbers\":[1]}";
            File.WriteAllText(repository.PrimaryPath, incompatible);

            SaveLoadResult result = repository.Load();

            Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.Incompatible));
            Assert.That(File.ReadAllText(repository.PrimaryPath), Is.EqualTo(incompatible));
        }

        [Test]
        public void SaveRepository_DeleteOwnedAutosave_PreservesUnownedFiles()
        {
            var repository = new LocalSaveRepository(testRoot);
            Assert.That(
                repository.Save(SaveRootV1.Create(new[] { 1 }, "first", "test")).IsSuccess,
                Is.True);
            Assert.That(
                repository.Save(SaveRootV1.Create(new[] { 1, 2 }, "second", "test")).IsSuccess,
                Is.True);

            string ownedTemp = Path.Combine(repository.SaveRoot, "autosave.abcd.tmp");
            string unownedInside = Path.Combine(repository.SaveRoot, "keep.txt");
            string unownedOutside = Path.Combine(testRoot, "outside.txt");
            File.WriteAllText(ownedTemp, "temporary");
            File.WriteAllText(unownedInside, "keep");
            File.WriteAllText(unownedOutside, "keep");

            SaveWriteResult result = repository.DeleteOwnedAutosave();

            Assert.That(result.IsSuccess, Is.True, result.Error);
            Assert.That(File.Exists(repository.PrimaryPath), Is.False);
            Assert.That(File.Exists(repository.BackupPath), Is.False);
            Assert.That(File.Exists(ownedTemp), Is.False);
            Assert.That(File.Exists(unownedInside), Is.True);
            Assert.That(File.Exists(unownedOutside), Is.True);
        }

        [Test]
        public void SaveWriteFailure_KeepsRuntimeUnlock_AndAllowsRetry()
        {
            string blockedRoot = Path.Combine(testRoot, "not-a-directory");
            File.WriteAllText(blockedRoot, "block directory creation");
            var repository = new LocalSaveRepository(blockedRoot);
            var coordinator = new SaveCoordinator(repository, "test");

            SaveLoadResult initialization = coordinator.Initialize();
            Assert.That(initialization.Status, Is.EqualTo(SaveLoadStatus.Missing));
            Assert.That(coordinator.LastWriteResult.IsSuccess, Is.False);

            UnlockAttemptResult unlock = coordinator.TryUnlockAndSave(2, out SaveWriteResult write);

            Assert.That(unlock, Is.EqualTo(UnlockAttemptResult.Unlocked));
            Assert.That(write.IsSuccess, Is.False);
            Assert.That(coordinator.Progress.IsUnlocked(2), Is.True);
            Assert.That(coordinator.RetrySave().IsSuccess, Is.False);
            Assert.That(coordinator.Progress.IsUnlocked(2), Is.True);
        }

        [Test]
        public void MissingLevelResult_ShowsRetry_AndDoesNotChangeUnlockProgress()
        {
            LevelCatalog catalog = ScriptableObject.CreateInstance<LevelCatalog>();
            GameObject loaderOwner = new GameObject("Level Loader Test");
            LevelSceneLoader loader = loaderOwner.AddComponent<LevelSceneLoader>();
            var save = new SaveCoordinator(new LocalSaveRepository(testRoot), "test");
            var ui = new RecordingApplicationUi();
            TowerNetworkManager towerNetworkManager = CreateTowerNetworkManager();
            var transitionFlow = new LevelTransitionFlow(loader, towerNetworkManager, ui);
            SetCatalogEntries(
                catalog,
                new LevelCatalogEntry(1, "Level 1", "Assets/Scenes/Levels/Missing.unity"),
                new LevelCatalogEntry(2, "Level 2", "Assets/Scenes/Levels/Level_002.unity"));
            var coordinator = new GameFlowCoordinator(
                ui,
                towerNetworkManager,
                new ApplicationBootFlow(catalog, save, ui),
                new LevelMenuFlow(catalog, save, ui),
                transitionFlow,
                new SaveRecoveryFlow(save, ui));

            try
            {
                coordinator.Start();
                InvokePrivate(
                    transitionFlow,
                    "OnLevelLoadCompleted",
                    new LevelLoadRequest(1, "Assets/Scenes/Levels/Missing.unity"),
                    new LevelTransitionResult(
                        LevelTransitionStatus.SceneNotInBuild,
                        1,
                        "Scene is missing from the player scene list."));

                Assert.That(coordinator.State, Is.EqualTo(GameFlowState.BlockingError));
                CollectionAssert.AreEqual(new[] { 1 }, save.Progress.CreateSortedSnapshot());
                Assert.That(ui.BlockingErrorMessage, Does.Contain("missing"));
                Assert.That(ui.Retry, Is.Not.Null);
                Assert.That(ui.StartNew, Is.Null);
            }
            finally
            {
                coordinator.Dispose();
                UnityEngine.Object.DestroyImmediate(loaderOwner);
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void LevelSceneContext_InitializesAndShutsDownParticipantsInContractOrder()
        {
            GameObject owner = new GameObject("Level Context Test");
            LevelSceneContext context = owner.AddComponent<LevelSceneContext>();
            RecordingParticipant first = owner.AddComponent<RecordingParticipant>();
            RecordingParticipant second = owner.AddComponent<RecordingParticipant>();
            SetLevelContext(context, 2, first, second);

            try
            {
                bool initialized = context.TryInitialize(
                    new LevelSceneRuntimeContext(2, () => { }),
                    out string error);

                Assert.That(initialized, Is.True, error);
                Assert.That(first.InitializeCount, Is.EqualTo(1));
                Assert.That(second.InitializeCount, Is.EqualTo(1));
                context.Shutdown();
                Assert.That(first.ShutdownCount, Is.EqualTo(1));
                Assert.That(second.ShutdownCount, Is.EqualTo(1));
                Assert.That(context.IsInitialized, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        private static void SetCatalogEntries(LevelCatalog catalog, params LevelCatalogEntry[] entries)
        {
            FieldInfo field = typeof(LevelCatalog).GetField(
                "levels",
                BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(catalog, new List<LevelCatalogEntry>(entries));
        }

        private static TowerNetworkManager CreateTowerNetworkManager()
        {
            TowerCatalog towerCatalog =
                AssetDatabase.LoadAssetAtPath<TowerCatalog>(TowerCatalogPath);

            Assert.That(
                towerCatalog,
                Is.Not.Null,
                $"Tower Catalog is missing at '{TowerCatalogPath}'.");

            return new TowerNetworkManager(towerCatalog);
        }

        private static void InvokePrivate(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "Missing private method " + methodName);
            method.Invoke(target, arguments);
        }

        private static void SetLevelContext(
            LevelSceneContext context,
            int levelNumber,
            params MonoBehaviour[] participants)
        {
            SerializedObject serialized = new SerializedObject(context);
            serialized.FindProperty("levelNumber").intValue = levelNumber;
            SerializedProperty participantArray = serialized.FindProperty("participants");
            participantArray.arraySize = participants.Length;
            for (int index = 0; index < participants.Length; index++)
            {
                participantArray.GetArrayElementAtIndex(index).objectReferenceValue = participants[index];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private sealed class RecordingParticipant : MonoBehaviour, ILevelSceneParticipant
        {
            public int InitializeCount { get; private set; }
            public int ShutdownCount { get; private set; }

            public void Initialize(LevelSceneRuntimeContext context)
            {
                InitializeCount++;
            }

            public void Shutdown()
            {
                ShutdownCount++;
            }
        }

        private sealed class RecordingApplicationUi : IApplicationUIController
        {
            public string BlockingErrorMessage { get; private set; }
            public Action Retry { get; private set; }
            public Action StartNew { get; private set; }

            public void Initialize()
            {
            }

            public void Shutdown()
            {
            }

            public void ShowLevelMenu(IReadOnlyList<LevelMenuItemState> levels, Action<int> onLevelSelected)
            {
            }

            public void HideLevelMenu()
            {
            }

            public void ShowLoading(string message)
            {
            }

            public void HideLoading()
            {
            }

            public void ShowBlockingError(string message, Action retry, Action startNew)
            {
                BlockingErrorMessage = message;
                Retry = retry;
                StartNew = startNew;
            }

            public void HideBlockingError()
            {
                BlockingErrorMessage = null;
                Retry = null;
                StartNew = null;
            }

            public void ShowSaveWarning(string message, Action retrySave)
            {
            }

            public void HideSaveWarning()
            {
            }

            public void SetInputBlocked(bool isBlocked)
            {
            }
        }
    }
}
