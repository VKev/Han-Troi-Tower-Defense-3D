using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TowerDefense3D.GameFlow.Editor;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.GameFlow.Tests.EditMode
{
    public sealed class GameFlowEditModeTests
    {
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
                SaveSnapshot.Create(new[] { 2, 1 }, "2026-08-15T00:00:00.0000000Z", "test"));
            SaveLoadResult load = repository.Load();

            Assert.That(write.IsSuccess, Is.True, write.Error);
            Assert.That(load.IsSuccess, Is.True, load.Error);
            CollectionAssert.AreEqual(new[] { 2, 1 }, load.Data.UnlockedLevelNumbers);
            Assert.That(load.Data.SlotId, Is.EqualTo(SaveSnapshot.AutosaveSlotId));
        }

        [Test]
        public void SaveRepository_CorruptPrimary_RecoversValidBackup()
        {
            var repository = new LocalSaveRepository(testRoot);
            Assert.That(
                repository.Save(SaveSnapshot.Create(new[] { 1 }, "first", "test")).IsSuccess,
                Is.True);
            Assert.That(
                repository.Save(SaveSnapshot.Create(new[] { 1, 2 }, "second", "test")).IsSuccess,
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
                repository.Save(SaveSnapshot.Create(new[] { 1 }, "first", "test")).IsSuccess,
                Is.True);
            Assert.That(
                repository.Save(SaveSnapshot.Create(new[] { 1, 2 }, "second", "test")).IsSuccess,
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
            var saveSystem = new SaveSystem(repository, "test");

            SaveLoadResult initialization = saveSystem.Initialize();
            Assert.That(initialization.Status, Is.EqualTo(SaveLoadStatus.Missing));
            Assert.That(saveSystem.LastWriteResult.IsSuccess, Is.False);

            UnlockAttemptResult unlock = saveSystem.TryUnlockAndSave(2, out SaveWriteResult write);

            Assert.That(unlock, Is.EqualTo(UnlockAttemptResult.Unlocked));
            Assert.That(write.IsSuccess, Is.False);
            Assert.That(saveSystem.Progress.IsUnlocked(2), Is.True);
            Assert.That(saveSystem.RetrySave().IsSuccess, Is.False);
            Assert.That(saveSystem.Progress.IsUnlocked(2), Is.True);
        }

        [Test]
        public void MissingLevelResult_ShowsRetry_AndDoesNotChangeUnlockProgress()
        {
            LevelCatalog catalog = ScriptableObject.CreateInstance<LevelCatalog>();
            var save = new SaveSystem(new LocalSaveRepository(testRoot), "test");
            var applicationView = new RecordingApplicationUIView();
            var ui = new ApplicationUISystem(applicationView);
            var transitionFlow = new LevelTransitionFlow(
                new LevelSceneSystem(new RecordingLevelSceneGateway()),
                ui);
            SetCatalogEntries(
                catalog,
                new LevelCatalogEntry(1, "Level 1", "Assets/Scenes/Levels/Missing.unity"),
                new LevelCatalogEntry(2, "Level 2", "Assets/Scenes/Levels/Level_002.unity"));
            var gameFlowSystem = new GameFlowSystem(
                new ApplicationBootFlow(catalog, save, ui),
                new LevelMenuFlow(catalog, save, ui),
                transitionFlow,
                new SaveRecoveryFlow(save, ui));

            try
            {
                gameFlowSystem.Start();
                InvokePrivate(
                    transitionFlow,
                    "OnLevelLoadCompleted",
                    new LevelLoadRequest(1, "Assets/Scenes/Levels/Missing.unity"),
                    new LevelTransitionResult(
                        LevelTransitionStatus.SceneNotInBuild,
                        1,
                        "Scene is missing from the player scene list."));

                Assert.That(gameFlowSystem.State, Is.EqualTo(GameFlowState.BlockingError));
                CollectionAssert.AreEqual(new[] { 1 }, save.Progress.CreateSortedSnapshot());
                Assert.That(applicationView.BlockingErrorMessage, Does.Contain("missing"));
                Assert.That(applicationView.Retry, Is.Not.Null);
                Assert.That(applicationView.StartNew, Is.Null);
            }
            finally
            {
                gameFlowSystem.Dispose();
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void LevelSceneSystem_LoadsAndUnloadsThroughGatewayInOrder()
        {
            var gateway = new RecordingLevelSceneGateway();
            var system = new LevelSceneSystem(gateway);
            LevelTransitionResult loadResult = default;
            LevelTransitionResult unloadResult = default;

            system.LoadLevel(
                new LevelLoadRequest(2, "Assets/Scenes/Levels/Level_002.unity"),
                result => loadResult = result);
            system.UnloadActiveLevel(result => unloadResult = result);

            Assert.That(loadResult.IsSuccess, Is.True, loadResult.Error);
            Assert.That(unloadResult.IsSuccess, Is.True, unloadResult.Error);
            CollectionAssert.AreEqual(new[] { "unload:0", "load:2", "unload:2" }, gateway.Operations);
            Assert.That(system.HasActiveLevel, Is.False);
        }

        [Test]
        public void LevelSceneSystem_RejectsInvalidRequestBeforeGatewayStarts()
        {
            var gateway = new RecordingLevelSceneGateway();
            var system = new LevelSceneSystem(gateway);
            LevelTransitionResult result = default;

            system.LoadLevel(default, observed => result = observed);

            Assert.That(result.Status, Is.EqualTo(LevelTransitionStatus.InvalidLevel));
            Assert.That(gateway.Operations, Is.Empty);
            Assert.That(system.IsTransitioning, Is.False);
        }

        [Test]
        public void LevelSceneSystem_RejectsRepeatedLoadWhileTransitionIsPending()
        {
            var gateway = new RecordingLevelSceneGateway { DelayLoad = true };
            var system = new LevelSceneSystem(gateway);
            var request = new LevelLoadRequest(1, "Assets/Scenes/Levels/Level_001.unity");
            LevelTransitionResult repeatedResult = default;

            system.LoadLevel(request, _ => { });
            system.LoadLevel(request, result => repeatedResult = result);

            Assert.That(repeatedResult.Status, Is.EqualTo(LevelTransitionStatus.Busy));
            CollectionAssert.AreEqual(new[] { "unload:0", "load:1" }, gateway.Operations);
            Assert.That(system.IsTransitioning, Is.True);

            gateway.CompletePendingLoad();

            Assert.That(system.IsTransitioning, Is.False);
            Assert.That(system.HasActiveLevel, Is.True);
        }

        private static void SetCatalogEntries(LevelCatalog catalog, params LevelCatalogEntry[] entries)
        {
            FieldInfo field = typeof(LevelCatalog).GetField(
                "levels",
                BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(catalog, new List<LevelCatalogEntry>(entries));
        }

        private static void InvokePrivate(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "Missing private method " + methodName);
            method.Invoke(target, arguments);
        }

        private sealed class RecordingLevelSceneGateway : ILevelSceneGateway
        {
            private LevelLoadRequest pendingRequest;
            private Action<LevelSceneHandle, LevelTransitionResult> pendingCompletion;

            public List<string> Operations { get; } = new List<string>();
            public bool DelayLoad { get; set; }

            public void LoadLevel(
                LevelLoadRequest request,
                Action<LevelSceneHandle, LevelTransitionResult> completion)
            {
                Operations.Add("load:" + request.LevelNumber);
                if (DelayLoad)
                {
                    pendingRequest = request;
                    pendingCompletion = completion;
                    return;
                }

                Complete(request, completion);
            }

            public void UnloadLevel(
                LevelSceneHandle handle,
                Action<LevelTransitionResult> completion)
            {
                Operations.Add("unload:" + handle.LevelNumber);
                completion(new LevelTransitionResult(
                    LevelTransitionStatus.Success,
                    handle.LevelNumber,
                    string.Empty));
            }

            public void CompletePendingLoad()
            {
                Action<LevelSceneHandle, LevelTransitionResult> completion = pendingCompletion;
                pendingCompletion = null;
                Complete(pendingRequest, completion);
            }

            private static void Complete(
                LevelLoadRequest request,
                Action<LevelSceneHandle, LevelTransitionResult> completion)
            {
                completion(
                    new LevelSceneHandle(request.LevelNumber, request.ScenePath, request.LevelNumber),
                    new LevelTransitionResult(
                        LevelTransitionStatus.Success,
                        request.LevelNumber,
                        string.Empty));
            }
        }

        private sealed class RecordingApplicationUIView : IApplicationUIView
        {
            public string BlockingErrorMessage { get; private set; }
            public Action Retry { get; private set; }
            public Action StartNew { get; private set; }

            public void Reset()
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
