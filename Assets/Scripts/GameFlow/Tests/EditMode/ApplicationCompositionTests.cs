using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TowerDefense3D.Towers;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.GameFlow.Tests.EditMode
{
    public sealed class ApplicationCompositionTests
    {
        private const string ApplicationUiPrefabPath =
            "Assets/Resources/Prefabs/ApplicationUI.prefab";
        private const string LevelCatalogPath =
            "Assets/Config/GameFlow/LevelCatalog.asset";
        private const string TowerCatalogPath =
            "Assets/Config/Towers/Catalogs/TowerCatalog.asset";

        private string testRoot;

        [SetUp]
        public void SetUp()
        {
            testRoot = Path.Combine(
                Path.GetTempPath(),
                "TowerDefense3D.ApplicationComposition.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testRoot);
        }

        [TearDown]
        public void TearDown()
        {
            string allowedRoot = Path.GetFullPath(
                Path.Combine(Path.GetTempPath(), "TowerDefense3D.ApplicationComposition.Tests"));
            string candidate = Path.GetFullPath(testRoot);
            if (candidate.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase)
                && Directory.Exists(candidate))
            {
                Directory.Delete(candidate, true);
            }
        }

        [Test]
        public void ApplicationComposition_UsesOnePlainCSharpEntryPoint()
        {
            var applicationEntryPoints = new List<Type>();
            Type[] runtimeTypes = typeof(ApplicationEntryPoint).Assembly.GetTypes();
            for (int index = 0; index < runtimeTypes.Length; index++)
            {
                if (ImplementsInterface(runtimeTypes[index], "VContainer.Unity.IAsyncStartable")
                    || ImplementsInterface(runtimeTypes[index], "VContainer.Unity.IStartable")
                    || ImplementsInterface(runtimeTypes[index], "VContainer.Unity.ITickable")
                    || ImplementsInterface(runtimeTypes[index], "VContainer.Unity.ILateTickable"))
                {
                    applicationEntryPoints.Add(runtimeTypes[index]);
                }
            }

            Assert.That(
                typeof(ApplicationLifetimeScope).BaseType?.FullName,
                Is.EqualTo("VContainer.Unity.LifetimeScope"));
            Assert.That(typeof(MonoBehaviour).IsAssignableFrom(typeof(GameFlowCoordinator)), Is.False);
            CollectionAssert.AreEqual(new[] { typeof(ApplicationEntryPoint) }, applicationEntryPoints);
            Assert.That(typeof(IDisposable).IsAssignableFrom(typeof(ApplicationEntryPoint)), Is.True);
            Assert.That(typeof(IDisposable).IsAssignableFrom(typeof(GameFlowCoordinator)), Is.False);
            Assert.That(typeof(MonoBehaviour).IsAssignableFrom(typeof(SaveCoordinator)), Is.False);
        }

        [Test]
        public void GameFlowCoordinator_StartAndDispose_ControlApplicationUiLifecycle()
        {
            LevelCatalog catalog = ScriptableObject.CreateInstance<LevelCatalog>();
            GameObject levelLoaderOwner = new GameObject("Level Loader Test");
            LevelSceneLoader levelSceneLoader = levelLoaderOwner.AddComponent<LevelSceneLoader>();
            var saveCoordinator = new SaveCoordinator(new LocalSaveRepository(testRoot), "test");
            var applicationUi = new RecordingApplicationUI();
            GameFlowCoordinator coordinator = CreateGameFlowCoordinator(
                catalog,
                saveCoordinator,
                CreateTowerNetworkManager(),
                levelSceneLoader,
                applicationUi);

            try
            {
                coordinator.Start();

                Assert.That(applicationUi.InitializeCount, Is.EqualTo(1));
                Assert.That(coordinator.State, Is.EqualTo(GameFlowState.BlockingError));
            }
            finally
            {
                coordinator.Dispose();
                UnityEngine.Object.DestroyImmediate(levelLoaderOwner);
                UnityEngine.Object.DestroyImmediate(catalog);
            }

            Assert.That(applicationUi.ShutdownCount, Is.EqualTo(1));
        }

        [Test]
        public void GameFlowCoordinator_FailedStart_RollsBackAndAllowsRetry()
        {
            LevelCatalog catalog = ScriptableObject.CreateInstance<LevelCatalog>();
            GameObject levelLoaderOwner = new GameObject("Level Loader Startup Failure Test");
            LevelSceneLoader levelSceneLoader = levelLoaderOwner.AddComponent<LevelSceneLoader>();
            var saveCoordinator = new SaveCoordinator(new LocalSaveRepository(testRoot), "test");
            var applicationUi = new RecordingApplicationUI();
            var expectedFailure = new InvalidOperationException("Expected startup failure.");
            applicationUi.ThrowOnNextShowLoading(expectedFailure);
            GameFlowCoordinator coordinator = CreateGameFlowCoordinator(
                catalog,
                saveCoordinator,
                CreateTowerNetworkManager(),
                levelSceneLoader,
                applicationUi);

            try
            {
                InvalidOperationException observedFailure = Assert.Throws<InvalidOperationException>(
                    () => coordinator.Start());

                Assert.That(observedFailure, Is.SameAs(expectedFailure));
                Assert.That(applicationUi.InitializeCount, Is.EqualTo(1));
                Assert.That(applicationUi.ShutdownCount, Is.EqualTo(1));

                coordinator.Start();

                Assert.That(applicationUi.InitializeCount, Is.EqualTo(2));
                Assert.That(applicationUi.ShutdownCount, Is.EqualTo(1));
                Assert.That(coordinator.State, Is.EqualTo(GameFlowState.BlockingError));
            }
            finally
            {
                coordinator.Dispose();
                UnityEngine.Object.DestroyImmediate(levelLoaderOwner);
                UnityEngine.Object.DestroyImmediate(catalog);
            }

            Assert.That(applicationUi.ShutdownCount, Is.EqualTo(2));
        }

        [Test]
        public void GameFlowCoordinator_FailedStartThenDispose_DoesNotRepeatRollback()
        {
            LevelCatalog catalog = ScriptableObject.CreateInstance<LevelCatalog>();
            GameObject levelLoaderOwner = new GameObject("Level Loader Failed Startup Disposal Test");
            LevelSceneLoader levelSceneLoader = levelLoaderOwner.AddComponent<LevelSceneLoader>();
            var saveCoordinator = new SaveCoordinator(new LocalSaveRepository(testRoot), "test");
            var applicationUi = new RecordingApplicationUI();
            applicationUi.ThrowOnNextShowLoading(new InvalidOperationException("Expected startup failure."));
            GameFlowCoordinator coordinator = CreateGameFlowCoordinator(
                catalog,
                saveCoordinator,
                CreateTowerNetworkManager(),
                levelSceneLoader,
                applicationUi);

            try
            {
                Assert.Throws<InvalidOperationException>(() => coordinator.Start());
                Assert.That(applicationUi.ShutdownCount, Is.EqualTo(1));
            }
            finally
            {
                coordinator.Dispose();
                UnityEngine.Object.DestroyImmediate(levelLoaderOwner);
                UnityEngine.Object.DestroyImmediate(catalog);
            }

            Assert.That(applicationUi.ShutdownCount, Is.EqualTo(1));
        }

        [Test]
        public void SaveCoordinator_UsesInjectedRepositoryWithoutUnityLifecycle()
        {
            var coordinator = new SaveCoordinator(new LocalSaveRepository(testRoot), "test");

            SaveLoadResult result = coordinator.Initialize();

            Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.Missing));
            Assert.That(coordinator.HasProgress, Is.True);
            Assert.That(coordinator.Progress.IsUnlocked(1), Is.True);
            Assert.That(
                typeof(SaveCoordinator).GetMethod(
                    "Awake",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly),
                Is.Null);
        }

        [Test]
        public void ApplicationUiManager_DoesNotInitializeFromAwake()
        {
            GameObject owner = PrefabUtility.LoadPrefabContents(ApplicationUiPrefabPath);
            ApplicationUIManager manager = owner.GetComponent<ApplicationUIManager>();

            try
            {
                Assert.That(manager, Is.Not.Null);
                Assert.That(manager.IsInitialized, Is.False);

                manager.Initialize();
                manager.Initialize();
                Assert.That(manager.IsInitialized, Is.True);

                manager.Shutdown();
                manager.Shutdown();
                Assert.That(manager.IsInitialized, Is.False);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(owner);
            }
        }

        [Test]
        public void ApplicationUiManager_ShutdownToleratesUnityDestructionOrder()
        {
            GameObject owner = PrefabUtility.LoadPrefabContents(ApplicationUiPrefabPath);
            ApplicationUIManager manager = owner.GetComponent<ApplicationUIManager>();
            LevelMenuScreen levelMenu = owner.GetComponentInChildren<LevelMenuScreen>(true);

            try
            {
                manager.Initialize();
                UnityEngine.Object.DestroyImmediate(levelMenu.gameObject);

                Assert.DoesNotThrow(manager.Shutdown);
                Assert.That(manager.IsInitialized, Is.False);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(owner);
            }
        }

        [Test]
        public void ApplicationUiPrefab_AuthorsOneReusableButtonPerCatalogLevel()
        {
            LevelCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(LevelCatalogPath);
            Assert.That(catalog, Is.Not.Null);
            GameObject owner = PrefabUtility.LoadPrefabContents(ApplicationUiPrefabPath);

            try
            {
                LevelMenuScreen screen = owner.GetComponentInChildren<LevelMenuScreen>(true);
                Assert.That(screen, Is.Not.Null);
                LevelButtonView[] buttons = GetPrivateField<LevelButtonView[]>(screen, "levelButtons");
                Assert.That(buttons, Has.Length.EqualTo(catalog.Levels.Count));
                Assert.That(owner.GetComponentsInChildren<LevelButtonView>(true), Has.Length.EqualTo(buttons.Length));

                var states = new List<LevelMenuItemState>(catalog.Levels.Count);
                for (int index = 0; index < catalog.Levels.Count; index++)
                {
                    LevelCatalogEntry entry = catalog.Levels[index];
                    states.Add(new LevelMenuItemState(
                        entry.LevelNumber,
                        entry.DisplayName,
                        isUnlocked: index == 0,
                        isBusy: false));
                }

                screen.Show(states, _ => { });
                screen.Show(states, _ => { });

                Assert.That(owner.GetComponentsInChildren<LevelButtonView>(true), Has.Length.EqualTo(buttons.Length));
                for (int index = 0; index < buttons.Length; index++)
                {
                    Assert.That(buttons[index], Is.Not.Null);
                    Assert.That(buttons[index].gameObject.activeSelf, Is.True);
                    Assert.That(GetPrivateField<int>(buttons[index], "levelNumber"),
                        Is.EqualTo(states[index].LevelNumber));
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(owner);
            }
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

        private static GameFlowCoordinator CreateGameFlowCoordinator(
            LevelCatalog catalog,
            SaveCoordinator saveCoordinator,
            TowerNetworkManager towerNetworkManager,
            LevelSceneLoader levelSceneLoader,
            IApplicationUIController applicationUi)
        {
            return new GameFlowCoordinator(
                applicationUi,
                towerNetworkManager,
                new ApplicationBootFlow(catalog, saveCoordinator, applicationUi),
                new LevelMenuFlow(catalog, saveCoordinator, applicationUi),
                new LevelTransitionFlow(levelSceneLoader, towerNetworkManager, applicationUi),
                new SaveRecoveryFlow(saveCoordinator, applicationUi));
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            return (T)field.GetValue(target);
        }

        private static bool ImplementsInterface(Type type, string interfaceFullName)
        {
            Type[] interfaces = type.GetInterfaces();
            for (int index = 0; index < interfaces.Length; index++)
            {
                if (string.Equals(interfaces[index].FullName, interfaceFullName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class RecordingApplicationUI : IApplicationUIController
        {
            private Exception nextShowLoadingException;

            public int InitializeCount { get; private set; }
            public int ShutdownCount { get; private set; }

            public void ThrowOnNextShowLoading(Exception exception)
            {
                nextShowLoadingException = exception;
            }

            public void Initialize()
            {
                InitializeCount++;
            }

            public void Shutdown()
            {
                ShutdownCount++;
            }

            public void ShowLevelMenu(IReadOnlyList<LevelMenuItemState> levels, Action<int> onLevelSelected)
            {
            }

            public void HideLevelMenu()
            {
            }

            public void ShowLoading(string message)
            {
                if (nextShowLoadingException == null)
                {
                    return;
                }

                Exception exception = nextShowLoadingException;
                nextShowLoadingException = null;
                throw exception;
            }

            public void HideLoading()
            {
            }

            public void ShowBlockingError(string message, Action retry, Action startNew)
            {
            }

            public void HideBlockingError()
            {
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
