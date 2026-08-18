using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TowerDefense3D.GridPlacement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow.Tests.PlayMode
{
    public sealed class GameFlowPlayModeTests
    {
        private const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";
        private const string LevelOneScenePath = "Assets/Scenes/Levels/Level_001.unity";
        private const string LevelTwoScenePath = "Assets/Scenes/Levels/Level_002.unity";
        private const int TransitionFrameBudget = 600;

        private string saveRoot;
        private string saveBackupRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            BackupRealSaveDirectory();

            AsyncOperation load = SceneManager.LoadSceneAsync(
                BootstrapScenePath,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            GameFlowCoordinator flow = FindLoaded<GameFlowCoordinator>();
            Assert.That(flow, Is.Not.Null);
            yield return WaitForState(flow, GameFlowState.LevelMenu);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Scene cleanupScene = SceneManager.CreateScene("GameFlow Test Cleanup");
            SceneManager.SetActiveScene(cleanupScene);

            var scenesToUnload = new List<Scene>();
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (scene.IsValid() && scene.isLoaded && scene != cleanupScene)
                {
                    scenesToUnload.Add(scene);
                }
            }

            for (int index = 0; index < scenesToUnload.Count; index++)
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(scenesToUnload[index]);
                if (unload != null)
                {
                    yield return unload;
                }
            }

            RestoreRealSaveDirectory();
        }

        [UnityTest]
        public IEnumerator Boot_ShowsLevelMenu_WithoutAutoLoadingLevel()
        {
            GameFlowCoordinator flow = FindLoaded<GameFlowCoordinator>();
            SaveCoordinator save = FindLoaded<SaveCoordinator>();

            Assert.That(flow.State, Is.EqualTo(GameFlowState.LevelMenu));
            Assert.That(save.Progress.IsUnlocked(1), Is.True);
            Assert.That(save.Progress.IsUnlocked(2), Is.False);
            Assert.That(IsSceneLoaded(LevelOneScenePath), Is.False);
            Assert.That(IsSceneLoaded(LevelTwoScenePath), Is.False);
            Assert.That(CountLoaded<LevelSceneContext>(), Is.Zero);
            Assert.That(FindLevelButton(1), Is.Not.Null);
            Assert.That(FindLevelButton(2), Is.Not.Null);
            yield break;
        }

        [UnityTest]
        public IEnumerator LevelOne_FirstTapLoads_AndReturnRestoresMenuWithoutDuplicates()
        {
            GameFlowCoordinator flow = FindLoaded<GameFlowCoordinator>();
            ClickLevel(1);
            yield return WaitForState(flow, GameFlowState.Gameplay);

            Assert.That(IsSceneLoaded(LevelOneScenePath), Is.True);
            Assert.That(IsSceneLoaded(LevelTwoScenePath), Is.False);
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(LevelOneScenePath));
            Assert.That(CountLoaded<EventSystem>(), Is.EqualTo(1));
            Assert.That(CountLoaded<Camera>(), Is.EqualTo(1));
            Assert.That(CountLoaded<AudioListener>(), Is.EqualTo(1));
            Assert.That(CountLoaded<GameFlowCoordinator>(), Is.EqualTo(1));
            Assert.That(CountLoaded<ApplicationUIManager>(), Is.EqualTo(1));
            Assert.That(CountLoaded<LevelSceneContext>(), Is.EqualTo(1));

            GameplayUIManager gameplayUi = FindLoaded<GameplayUIManager>();
            Assert.That(gameplayUi, Is.Not.Null);
            Assert.That(gameplayUi.IsInitialized, Is.True);
            AssertMigratedGameplayUi(gameplayUi, "Level_001_Board");

            GetPrivateField<Button>(gameplayUi, "returnToMenuButton").onClick.Invoke();
            yield return WaitForState(flow, GameFlowState.LevelMenu);

            Assert.That(IsSceneLoaded(LevelOneScenePath), Is.False);
            Assert.That(CountLoaded<LevelSceneContext>(), Is.Zero);
            Assert.That(CountLoaded<EventSystem>(), Is.EqualTo(1));
            Assert.That(CountLoaded<GameFlowCoordinator>(), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator LevelTwo_FirstTapUnlocks_SecondTapLoads_AndUnlockPersists()
        {
            GameFlowCoordinator flow = FindLoaded<GameFlowCoordinator>();
            SaveCoordinator save = FindLoaded<SaveCoordinator>();

            ClickLevel(2);
            yield return null;
            yield return null;

            Assert.That(flow.State, Is.EqualTo(GameFlowState.LevelMenu));
            Assert.That(save.Progress.IsUnlocked(2), Is.True);
            Assert.That(IsSceneLoaded(LevelTwoScenePath), Is.False);
            Assert.That(FindLevelButton(2), Is.Not.Null);

            ClickLevel(2);
            yield return WaitForState(flow, GameFlowState.Gameplay);

            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(LevelTwoScenePath));
            GameplayUIManager gameplayUi = FindLoaded<GameplayUIManager>();
            AssertMigratedGameplayUi(gameplayUi, "Level_002_Board");

            SaveLoadResult persisted = new LocalSaveRepository(Application.persistentDataPath).Load();
            Assert.That(persisted.IsSuccess, Is.True, persisted.Error);
            CollectionAssert.Contains(persisted.Data.UnlockedLevelNumbers, 2);

            GetPrivateField<Button>(gameplayUi, "returnToMenuButton").onClick.Invoke();
            yield return WaitForState(flow, GameFlowState.LevelMenu);
        }

        [UnityTest]
        public IEnumerator MissingLevel_ShowsRetry_AndDoesNotChangeUnlockProgress()
        {
            GameFlowCoordinator flow = FindLoaded<GameFlowCoordinator>();
            SaveCoordinator save = FindLoaded<SaveCoordinator>();
            LevelCatalog original = GetPrivateField<LevelCatalog>(flow, "levelCatalog");
            LevelCatalog invalid = UnityEngine.Object.Instantiate(original);
            SetCatalogEntries(
                invalid,
                new LevelCatalogEntry(1, "Level 1", "Assets/Scenes/Levels/Missing.unity"),
                new LevelCatalogEntry(2, "Level 2", LevelTwoScenePath));
            SetPrivateField(flow, "levelCatalog", invalid);

            try
            {
                ClickLevel(1);
                yield return WaitForState(flow, GameFlowState.BlockingError);

                CollectionAssert.AreEqual(new[] { 1 }, save.Progress.CreateSortedSnapshot());
                Assert.That(IsSceneLoaded(LevelOneScenePath), Is.False);
                Assert.That(IsSceneLoaded(LevelTwoScenePath), Is.False);

                BlockingErrorScreen error = FindLoaded<BlockingErrorScreen>();
                Assert.That(error, Is.Not.Null);
                Button retry = GetPrivateField<Button>(error, "retryButton");
                Button startNew = GetPrivateField<Button>(error, "startNewButton");
                Assert.That(retry.gameObject.activeInHierarchy, Is.True);
                Assert.That(startNew.gameObject.activeSelf, Is.False);

                retry.onClick.Invoke();
                yield return null;
                yield return WaitForState(flow, GameFlowState.BlockingError);
                CollectionAssert.AreEqual(new[] { 1 }, save.Progress.CreateSortedSnapshot());
            }
            finally
            {
                UnityEngine.Object.Destroy(invalid);
            }
        }

        private static void AssertMigratedGameplayUi(
            GameplayUIManager manager,
            string expectedBoardName)
        {
            Assert.That(manager, Is.Not.Null);
            GridPlacementController placement = GetPrivateField<GridPlacementController>(
                manager,
                "placementController");
            TowerSelectionButton[] selectors = GetPrivateField<TowerSelectionButton[]>(
                manager,
                "towerSelectionButtons");
            Button cancel = GetPrivateField<Button>(manager, "cancelPlacementButton");
            Button returnButton = GetPrivateField<Button>(manager, "returnToMenuButton");

            Assert.That(placement, Is.Not.Null);
            Assert.That(GetPrivateField<BoardDefinition>(placement, "boardDefinition").name,
                Is.EqualTo(expectedBoardName));
            Assert.That(selectors, Has.Length.EqualTo(1));
            Assert.That(selectors[0].Definition, Is.Not.Null);
            Assert.That(cancel, Is.Not.Null);
            Assert.That(returnButton, Is.Not.Null);
            Assert.That(manager.GetComponentInChildren<SafeAreaFitter>(true), Is.Not.Null);
        }

        private static IEnumerator WaitForState(
            GameFlowCoordinator flow,
            GameFlowState expected)
        {
            for (int frame = 0; frame < TransitionFrameBudget; frame++)
            {
                if (flow != null && flow.State == expected)
                {
                    yield break;
                }

                if (flow != null
                    && flow.State == GameFlowState.BlockingError
                    && expected != GameFlowState.BlockingError)
                {
                    BlockingErrorScreen error = FindLoaded<BlockingErrorScreen>();
                    Text message = error != null
                        ? GetPrivateField<Text>(error, "messageLabel")
                        : null;
                    Assert.Fail(
                        "Entered BlockingError while waiting for "
                        + expected
                        + ": "
                        + (message != null ? message.text : "missing error message"));
                }

                yield return null;
            }

            Assert.Fail(
                "Timed out waiting for GameFlowState. Expected "
                + expected
                + ", current "
                + (flow != null ? flow.State.ToString() : "missing flow"));
        }

        private static void ClickLevel(int levelNumber)
        {
            LevelButtonView view = FindLevelButton(levelNumber);
            Assert.That(view, Is.Not.Null, "Missing bound level button " + levelNumber);
            view.GetComponent<Button>().onClick.Invoke();
        }

        private static LevelButtonView FindLevelButton(int levelNumber)
        {
            LevelButtonView[] views = Resources.FindObjectsOfTypeAll<LevelButtonView>();
            for (int index = 0; index < views.Length; index++)
            {
                LevelButtonView view = views[index];
                if (IsLoadedSceneObject(view)
                    && GetPrivateField<int>(view, "levelNumber") == levelNumber)
                {
                    return view;
                }
            }

            return null;
        }

        private static T FindLoaded<T>() where T : Component
        {
            T[] values = Resources.FindObjectsOfTypeAll<T>();
            for (int index = 0; index < values.Length; index++)
            {
                if (IsLoadedSceneObject(values[index]))
                {
                    return values[index];
                }
            }

            return null;
        }

        private static int CountLoaded<T>() where T : Component
        {
            int count = 0;
            T[] values = Resources.FindObjectsOfTypeAll<T>();
            for (int index = 0; index < values.Length; index++)
            {
                if (IsLoadedSceneObject(values[index]))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool IsLoadedSceneObject(Component component)
        {
            return component != null
                && component.gameObject.scene.IsValid()
                && component.gameObject.scene.isLoaded;
        }

        private static bool IsSceneLoaded(string path)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            return scene.IsValid() && scene.isLoaded;
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Missing private field " + fieldName);
            return (T)field.GetValue(target);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Missing private field " + fieldName);
            field.SetValue(target, value);
        }

        private static void SetCatalogEntries(
            LevelCatalog catalog,
            params LevelCatalogEntry[] entries)
        {
            FieldInfo field = typeof(LevelCatalog).GetField(
                "levels",
                BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(catalog, new List<LevelCatalogEntry>(entries));
        }

        private void BackupRealSaveDirectory()
        {
            var repository = new LocalSaveRepository(Application.persistentDataPath);
            saveRoot = repository.SaveRoot;
            saveBackupRoot = saveRoot + ".playmode-backup-" + Guid.NewGuid().ToString("N");
            if (Directory.Exists(saveRoot))
            {
                Directory.Move(saveRoot, saveBackupRoot);
            }
        }

        private void RestoreRealSaveDirectory()
        {
            if (!string.IsNullOrEmpty(saveRoot) && Directory.Exists(saveRoot))
            {
                Directory.Delete(saveRoot, true);
            }

            if (!string.IsNullOrEmpty(saveBackupRoot) && Directory.Exists(saveBackupRoot))
            {
                Directory.Move(saveBackupRoot, saveRoot);
            }

            saveRoot = null;
            saveBackupRoot = null;
        }
    }
}
