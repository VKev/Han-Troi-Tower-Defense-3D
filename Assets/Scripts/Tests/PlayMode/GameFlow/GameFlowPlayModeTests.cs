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
        // The tower bar shows one button per element plus the generator, the soul nexus and the
        // hero. It is not reachable from a player build, so it is named here rather than left as a
        // bare number. The level menu's count is read from the catalog instead - see
        // <see cref="GetAuthoredLevelCount"/> - because that one grows every time a level is added.
        private const int ElementCount = 3;
        private const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";
        private const string LevelOneScenePath = "Assets/Scenes/Levels/Level_001.unity";
        private const string LevelTwoScenePath = "Assets/Scenes/Levels/Level_002.unity";
        private const string LevelSevenScenePath = "Assets/Scenes/Levels/Level_007.unity";
        private const string ApplicationLifetimeScopeTypeName =
            "TowerDefense3D.GameFlow.ApplicationLifetimeScope";
        private const string LevelLifetimeScopeTypeName =
            "TowerDefense3D.GameFlow.LevelLifetimeScope";
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
            Assert.That(CountLoadedByFullName(LevelLifetimeScopeTypeName), Is.Zero);
            Assert.That(CountLoaded<LevelButtonView>(), Is.EqualTo(GetAuthoredLevelCount()));
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
            Assert.That(CountLoadedByFullName(LevelLifetimeScopeTypeName), Is.EqualTo(1));
            AssertTowerNetworkInitialized();

            GameplayUIView gameplayUi = FindLoaded<GameplayUIView>();
            Assert.That(gameplayUi, Is.Not.Null);
            Assert.That(gameplayUi.IsVisible, Is.True);
            AssertMigratedGameplayUi(gameplayUi, "Level_001_Board");

            GetReturnToMenuButton(gameplayUi).onClick.Invoke();
            yield return WaitForLevelMenu();

            Assert.That(IsSceneLoaded(LevelOneScenePath), Is.False);
            Assert.That(CountLoadedByFullName(LevelLifetimeScopeTypeName), Is.Zero);
            Assert.That(CountLoaded<GridPlacementPresenter>(), Is.Zero);
            Assert.That(CountLoaded<TowerLinkView>(), Is.Zero);
            Assert.That(CountLoaded<TowerProjectilePoolView>(), Is.Zero);
            Assert.That(CountLoaded<EventSystem>(), Is.EqualTo(1));
            Assert.That(CountLoadedByFullName(ApplicationLifetimeScopeTypeName), Is.EqualTo(1));
            Assert.That(CountLoaded<LevelButtonView>(), Is.EqualTo(GetAuthoredLevelCount()));
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

        [UnityTest]
        public IEnumerator LevelSeven_AdoptsItsAuthoredHero_IntoTheTowerNetwork()
        {
            ClickLevel(7);
            yield return WaitForLevelButtonLabel(7, "Play ");
            ClickLevel(7);
            yield return WaitForGameplay(LevelSevenScenePath);

            AuthoredTowerView authoredHero = FindLoaded<AuthoredTowerView>();
            Assert.That(authoredHero, Is.Not.Null, "Level 7 must author the crab hero.");
            Assert.That(authoredHero.Definition, Is.InstanceOf<HeroTowerDefinition>());

            TowerRuntimeView heroView = authoredHero.RuntimeView;
            Assert.That(heroView.IsConfigured, Is.True, "the authored hero must be configured");
            Assert.That(heroView.IsRegistered, Is.True, "the authored hero must own a network node");

            // The hero holds the cells under its own footprint, so nothing can be dropped on it.
            Assert.That(
                heroView.CombatDefinition.Core.PlacementDefinition.Footprint,
                Is.EqualTo(new TowerFootprint(3, 3, 2)));

            GameplayUIView gameplayUi = FindLoaded<GameplayUIView>();
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
            int elementButtons = 0;
            int generatorButtons = 0;
            int nexusButtons = 0;
            int heroButtons = 0;
            int lockedHeroButtons = 0;
            for (int index = 0; index < dragButtons.Length; index++)
            {
                TowerPlacementDragButtonView dragButton = dragButtons[index];
                TowerCombatDefinition definition = dragButton.Definition;
                if (definition is ElementTowerDefinition)
                {
                    elementButtons++;
                }
                else if (definition is GeneratorTowerDefinition)
                {
                    generatorButtons++;
                }
                else if (definition is SoulNexusDefinition)
                {
                    nexusButtons++;
                }
                else if (definition is HeroTowerDefinition)
                {
                    heroButtons++;
                    lockedHeroButtons += dragButton.IsLocked ? 1 : 0;
                }
            }

            // Checking what the bar is made of rather than how many buttons it has means a missing
            // or stray element reads as exactly that, instead of as a count to puzzle over.
            Assert.That(elementButtons, Is.EqualTo(ElementCount), "one drag button per element");
            Assert.That(generatorButtons, Is.EqualTo(1), "one generator drag button");
            Assert.That(nexusButtons, Is.EqualTo(1), "one soul nexus drag button");
            Assert.That(heroButtons, Is.EqualTo(1), "one hero drag button");

            // These levels run on a fresh save, so the hero's unlock level has not been cleared and
            // its button has to load greyed out rather than be missing from the bar.
            Assert.That(lockedHeroButtons, Is.EqualTo(1), "the hero drag button loads locked");
            Assert.That(dragButtons, Has.Length.EqualTo(ElementCount + 3));
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
            Assert.That(CountLoaded<GridPlacementPresenter>(), Is.EqualTo(1));
            Assert.That(CountLoaded<TowerLinkView>(), Is.EqualTo(1));
            Assert.That(CountLoaded<TowerProjectilePoolView>(), Is.EqualTo(1));

            GridPlacementPresenter presenter = FindLoaded<GridPlacementPresenter>();

            Assert.That(presenter, Is.Not.Null);
            Assert.That(presenter.IsInitialized, Is.True);
            Assert.That(presenter.GetComponent<TowerLinkView>(), Is.Not.Null);
            Assert.That(presenter.GetComponent<TowerProjectilePoolView>(), Is.Not.Null);
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

        /// <summary>
        /// One menu button per authored level, read from the catalog the booted application is
        /// actually running on. Hard-coding the number here means every new level breaks these
        /// tests for a reason that has nothing to do with what they cover.
        /// </summary>
        private static int GetAuthoredLevelCount()
        {
            Component[] values = Resources.FindObjectsOfTypeAll<Component>();
            for (int index = 0; index < values.Length; index++)
            {
                Component value = values[index];
                if (!IsLoadedSceneObject(value)
                    || !string.Equals(
                        value.GetType().FullName,
                        ApplicationLifetimeScopeTypeName,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                var catalog = GetPrivateField<LevelCatalog>(value, "levelCatalog");
                Assert.That(catalog, Is.Not.Null, "The application scope must author a LevelCatalog.");
                return catalog.Levels.Count;
            }

            throw new InvalidOperationException(
                "No loaded ApplicationLifetimeScope to read the level catalog from.");
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
