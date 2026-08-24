using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TowerDefense3D.GridPlacement;
using TowerDefense3D.Towers;
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
        private const string ApplicationLifetimeScopeTypeName =
            "TowerDefense3D.GameFlow.ApplicationLifetimeScope";
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

            yield return WaitForLevelMenu();
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
            ApplicationUIView applicationView = FindLoaded<ApplicationUIView>();

            Assert.That(applicationView, Is.Not.Null);
            Assert.That(CountLoadedByFullName(ApplicationLifetimeScopeTypeName), Is.EqualTo(1));
            Assert.That(IsSceneLoaded(LevelOneScenePath), Is.False);
            Assert.That(IsSceneLoaded(LevelTwoScenePath), Is.False);
            Assert.That(CountLoaded<LevelSceneContext>(), Is.Zero);
            Assert.That(CountLoaded<LevelButtonView>(), Is.EqualTo(2));
            Assert.That(CountLoaded<SafeAreaView>(), Is.EqualTo(1));
            Assert.That(
                Application.targetFrameRate,
                Is.EqualTo(TowerDefense3D.Mobile.FramePacingSystem.TargetFrameRate));
            Assert.That(QualitySettings.vSyncCount, Is.Zero);
            Assert.That(GetLevelButtonLabel(1), Does.StartWith("Play "));
            Assert.That(GetLevelButtonLabel(2), Does.StartWith("Unlock "));
            yield break;
        }

        [UnityTest]
        public IEnumerator LevelOne_FirstTapLoads_AndReturnRestoresMenuWithoutDuplicates()
        {
            ClickLevel(1);
            yield return WaitForGameplay(LevelOneScenePath);

            Assert.That(IsSceneLoaded(LevelOneScenePath), Is.True);
            Assert.That(IsSceneLoaded(LevelTwoScenePath), Is.False);
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(LevelOneScenePath));
            Assert.That(CountLoaded<EventSystem>(), Is.EqualTo(1));
            Assert.That(CountLoaded<Camera>(), Is.EqualTo(1));
            Assert.That(CountLoaded<AudioListener>(), Is.EqualTo(1));
            Assert.That(CountLoadedByFullName(ApplicationLifetimeScopeTypeName), Is.EqualTo(1));
            Assert.That(CountLoaded<ApplicationUIView>(), Is.EqualTo(1));
            Assert.That(CountLoaded<LevelSceneContext>(), Is.EqualTo(1));
            AssertTowerNetworkInitialized();

            GameplayUIView gameplayUi = FindLoaded<GameplayUIView>();
            Assert.That(gameplayUi, Is.Not.Null);
            Assert.That(gameplayUi.IsVisible, Is.True);
            AssertMigratedGameplayUi(gameplayUi, "Level_001_Board");

            GetReturnToMenuButton(gameplayUi).onClick.Invoke();
            yield return WaitForLevelMenu();

            Assert.That(IsSceneLoaded(LevelOneScenePath), Is.False);
            Assert.That(CountLoaded<LevelSceneContext>(), Is.Zero);
            Assert.That(CountLoaded<TowerNetworkSceneAdapter>(), Is.Zero);
            Assert.That(CountLoaded<TowerLinkView>(), Is.Zero);
            Assert.That(CountLoaded<TowerProjectilePoolView>(), Is.Zero);
            Assert.That(CountLoaded<EventSystem>(), Is.EqualTo(1));
            Assert.That(CountLoadedByFullName(ApplicationLifetimeScopeTypeName), Is.EqualTo(1));
            Assert.That(CountLoaded<LevelButtonView>(), Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator LevelTwo_FirstTapUnlocks_SecondTapLoads_AndUnlockPersists()
        {
            ClickLevel(2);
            yield return WaitForLevelButtonLabel(2, "Play ");

            Assert.That(IsSceneLoaded(LevelTwoScenePath), Is.False);
            Assert.That(FindLevelButton(2), Is.Not.Null);

            ClickLevel(2);
            yield return WaitForGameplay(LevelTwoScenePath);

            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(LevelTwoScenePath));
            AssertTowerNetworkInitialized();
            GameplayUIView gameplayUi = FindLoaded<GameplayUIView>();
            AssertMigratedGameplayUi(gameplayUi, "Level_002_Board");

            SaveLoadResult persisted = new LocalSaveRepository(Application.persistentDataPath).Load();
            Assert.That(persisted.IsSuccess, Is.True, persisted.Error);
            CollectionAssert.Contains(persisted.Data.UnlockedLevelNumbers, 2);

            GetReturnToMenuButton(gameplayUi).onClick.Invoke();
            yield return WaitForLevelMenu();
        }

        private static void AssertMigratedGameplayUi(
            GameplayUIView view,
            string expectedBoardName)
        {
            Assert.That(view, Is.Not.Null);
            TowerNetworkHudView hud = view.GetComponentInChildren<TowerNetworkHudView>(true);
            TowerPlacementDragButtonView[] dragButtons =
                view.GetComponentsInChildren<TowerPlacementDragButtonView>(true);
            Button cancel = GetCancelPlacementButton(view);
            Button returnButton = GetReturnToMenuButton(view);

            Assert.That(hud, Is.Not.Null);
            Assert.That(hud.IsInitialized, Is.True);
            BoardView boardView = UnityEngine.Object.FindFirstObjectByType<BoardView>();
            Assert.That(boardView, Is.Not.Null);
            Assert.That(boardView.Board.name, Is.EqualTo(expectedBoardName));
            Assert.That(dragButtons, Has.Length.EqualTo(6));
            Assert.That(view.transform.Find("Safe Area/Select Tower"), Is.Null);
            Assert.That(cancel, Is.Not.Null);
            Assert.That(returnButton, Is.Not.Null);
            Assert.That(view.GetComponentInChildren<SafeAreaView>(true), Is.Not.Null);
        }

        private static Button GetCancelPlacementButton(GameplayUIView view)
        {
            TowerNetworkHudView hud = view.GetComponentInChildren<TowerNetworkHudView>(true);
            return GetPrivateField<Button>(hud, "cancelPlacementButton");
        }

        private static Button GetReturnToMenuButton(GameplayUIView view)
        {
            TowerNetworkHudView hud = view.GetComponentInChildren<TowerNetworkHudView>(true);
            return GetPrivateField<Button>(hud, "returnToMenuButton");
        }

        private static void AssertTowerNetworkInitialized()
        {
            Assert.That(CountLoaded<TowerNetworkSceneAdapter>(), Is.EqualTo(1));
            Assert.That(CountLoaded<TowerLinkView>(), Is.EqualTo(1));
            Assert.That(CountLoaded<TowerProjectilePoolView>(), Is.EqualTo(1));

            TowerNetworkSceneAdapter adapter = FindLoaded<TowerNetworkSceneAdapter>();

            Assert.That(adapter, Is.Not.Null);
            Assert.That(adapter.IsInitialized, Is.True);
            Assert.That(adapter.CanEditTopology, Is.True);
            Assert.That(adapter.GetComponent<TowerLinkView>(), Is.Not.Null);
            Assert.That(adapter.GetComponent<TowerProjectilePoolView>(), Is.Not.Null);
        }

        private static IEnumerator WaitForLevelMenu()
        {
            for (int frame = 0; frame < TransitionFrameBudget; frame++)
            {
                FailIfBlockingError("level menu");
                ApplicationUIView applicationView = FindLoaded<ApplicationUIView>();
                LevelButtonView levelOne = FindLevelButton(1);
                LevelButtonView levelTwo = FindLevelButton(2);
                if (applicationView != null
                    && levelOne != null
                    && levelOne.gameObject.activeInHierarchy
                    && levelTwo != null
                    && levelTwo.gameObject.activeInHierarchy)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("Timed out waiting for the level menu.");
        }

        private static IEnumerator WaitForGameplay(string scenePath)
        {
            for (int frame = 0; frame < TransitionFrameBudget; frame++)
            {
                FailIfBlockingError("gameplay scene " + scenePath);
                GameplayUIView gameplayUi = FindLoaded<GameplayUIView>();
                TowerNetworkHudView hud = gameplayUi != null
                    ? gameplayUi.GetComponentInChildren<TowerNetworkHudView>(true)
                    : null;
                if (IsSceneLoaded(scenePath)
                    && SceneManager.GetActiveScene().path == scenePath
                    && gameplayUi != null
                    && gameplayUi.IsVisible
                    && hud != null
                    && hud.IsInitialized)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("Timed out waiting for gameplay scene " + scenePath + ".");
        }

        private static IEnumerator WaitForLevelButtonLabel(int levelNumber, string prefix)
        {
            for (int frame = 0; frame < TransitionFrameBudget; frame++)
            {
                FailIfBlockingError("level button " + levelNumber);
                string label = GetLevelButtonLabel(levelNumber);
                if (!string.IsNullOrEmpty(label) && label.StartsWith(prefix, StringComparison.Ordinal))
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("Timed out waiting for level button label " + levelNumber + ".");
        }

        private static void FailIfBlockingError(string expected)
        {
            BlockingErrorView error = FindLoaded<BlockingErrorView>();
            if (error == null || !error.gameObject.activeInHierarchy)
            {
                return;
            }

            Text message = GetPrivateField<Text>(error, "messageLabel");
            Assert.Fail(
                "Entered BlockingError while waiting for "
                + expected
                + ": "
                + (message != null ? message.text : "missing error message"));
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

        private static string GetLevelButtonLabel(int levelNumber)
        {
            LevelButtonView view = FindLevelButton(levelNumber);
            Text label = view != null ? GetPrivateField<Text>(view, "label") : null;
            return label != null ? label.text : string.Empty;
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

        private static int CountLoadedByFullName(string fullName)
        {
            int count = 0;
            Component[] values = Resources.FindObjectsOfTypeAll<Component>();
            for (int index = 0; index < values.Length; index++)
            {
                Component value = values[index];
                if (IsLoadedSceneObject(value)
                    && string.Equals(value.GetType().FullName, fullName, StringComparison.Ordinal))
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
