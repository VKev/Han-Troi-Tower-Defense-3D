using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TowerDefense3D.GridPlacement;
using TowerDefense3D.Mobile;
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
            Assert.That(typeof(MonoBehaviour).IsAssignableFrom(typeof(GameFlowSystem)), Is.False);
            CollectionAssert.AreEqual(new[] { typeof(ApplicationEntryPoint) }, applicationEntryPoints);
            Assert.That(typeof(IDisposable).IsAssignableFrom(typeof(ApplicationEntryPoint)), Is.True);
            Assert.That(typeof(IDisposable).IsAssignableFrom(typeof(GameFlowSystem)), Is.False);
            Assert.That(typeof(MonoBehaviour).IsAssignableFrom(typeof(SaveSystem)), Is.False);
            Assert.That(typeof(MonoBehaviour).IsAssignableFrom(typeof(ApplicationUISystem)), Is.False);
        }

        [Test]
        public void ApplicationSystemGroup_StartAndShutdown_ControlApplicationUiLifecycle()
        {
            LevelCatalog catalog = ScriptableObject.CreateInstance<LevelCatalog>();
            var saveSystem = new SaveSystem(new LocalSaveRepository(testRoot), "test");
            var applicationView = new RecordingApplicationUIView();
            var applicationUiSystem = new ApplicationUISystem(applicationView);
            GameFlowSystem gameFlowSystem = CreateGameFlowSystem(catalog, saveSystem, applicationUiSystem);
            ApplicationSystemGroup systems = CreateApplicationSystemGroup(gameFlowSystem, applicationUiSystem);

            try
            {
                systems.Start();

                Assert.That(applicationUiSystem.IsStarted, Is.True);
                Assert.That(gameFlowSystem.State, Is.EqualTo(GameFlowState.BlockingError));
            }
            finally
            {
                systems.Shutdown();
                UnityEngine.Object.DestroyImmediate(catalog);
            }

            Assert.That(applicationUiSystem.IsStarted, Is.False);
        }

        [Test]
        public void ApplicationSystemGroup_FailedStart_RollsBackAndAllowsRetry()
        {
            LevelCatalog catalog = ScriptableObject.CreateInstance<LevelCatalog>();
            var saveSystem = new SaveSystem(new LocalSaveRepository(testRoot), "test");
            var applicationView = new RecordingApplicationUIView();
            var applicationUiSystem = new ApplicationUISystem(applicationView);
            var expectedFailure = new InvalidOperationException("Expected startup failure.");
            applicationView.ThrowOnNextShowLoading(expectedFailure);
            GameFlowSystem gameFlowSystem = CreateGameFlowSystem(catalog, saveSystem, applicationUiSystem);
            ApplicationSystemGroup systems = CreateApplicationSystemGroup(gameFlowSystem, applicationUiSystem);

            try
            {
                InvalidOperationException observedFailure = Assert.Throws<InvalidOperationException>(
                    systems.Start);

                Assert.That(observedFailure, Is.SameAs(expectedFailure));
                Assert.That(applicationUiSystem.IsStarted, Is.False);

                systems.Start();

                Assert.That(applicationUiSystem.IsStarted, Is.True);
                Assert.That(gameFlowSystem.State, Is.EqualTo(GameFlowState.BlockingError));
            }
            finally
            {
                systems.Shutdown();
                UnityEngine.Object.DestroyImmediate(catalog);
            }

            Assert.That(applicationUiSystem.IsStarted, Is.False);
        }

        [Test]
        public void ApplicationSystemGroup_FailedStartThenShutdown_DoesNotRepeatRollback()
        {
            LevelCatalog catalog = ScriptableObject.CreateInstance<LevelCatalog>();
            var saveSystem = new SaveSystem(new LocalSaveRepository(testRoot), "test");
            var applicationView = new RecordingApplicationUIView();
            var applicationUiSystem = new ApplicationUISystem(applicationView);
            applicationView.ThrowOnNextShowLoading(new InvalidOperationException("Expected startup failure."));
            GameFlowSystem gameFlowSystem = CreateGameFlowSystem(catalog, saveSystem, applicationUiSystem);
            ApplicationSystemGroup systems = CreateApplicationSystemGroup(gameFlowSystem, applicationUiSystem);

            try
            {
                Assert.Throws<InvalidOperationException>(systems.Start);
                Assert.That(applicationUiSystem.IsStarted, Is.False);
            }
            finally
            {
                systems.Shutdown();
                UnityEngine.Object.DestroyImmediate(catalog);
            }

            Assert.That(applicationUiSystem.IsStarted, Is.False);
        }

        [Test]
        public void SaveSystem_UsesInjectedRepositoryWithoutUnityLifecycle()
        {
            var saveSystem = new SaveSystem(new LocalSaveRepository(testRoot), "test");

            SaveLoadResult result = saveSystem.Initialize();

            Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.Missing));
            Assert.That(saveSystem.HasProgress, Is.True);
            Assert.That(saveSystem.Progress.IsUnlocked(1), Is.True);
            Assert.That(
                typeof(SaveSystem).GetMethod(
                    "Awake",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly),
                Is.Null);
        }

        [Test]
        public void ApplicationUISystem_ControlsAuthoredViewWithoutMonoLifecycle()
        {
            GameObject owner = PrefabUtility.LoadPrefabContents(ApplicationUiPrefabPath);
            ApplicationUIView view = owner.GetComponent<ApplicationUIView>();
            var system = new ApplicationUISystem(view);

            try
            {
                Assert.That(view, Is.Not.Null);
                Assert.That(system.IsStarted, Is.False);

                system.Start();
                system.Start();
                Assert.That(system.IsStarted, Is.True);

                system.Dispose();
                system.Dispose();
                Assert.That(system.IsStarted, Is.False);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(owner);
            }
        }

        [Test]
        public void ApplicationUISystem_DisposeToleratesUnityDestructionOrder()
        {
            GameObject owner = PrefabUtility.LoadPrefabContents(ApplicationUiPrefabPath);
            ApplicationUIView view = owner.GetComponent<ApplicationUIView>();
            LevelMenuView levelMenuView = owner.GetComponentInChildren<LevelMenuView>(true);
            var system = new ApplicationUISystem(view);

            try
            {
                system.Start();
                UnityEngine.Object.DestroyImmediate(levelMenuView.gameObject);

                Assert.DoesNotThrow(system.Dispose);
                Assert.That(system.IsStarted, Is.False);
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
                LevelMenuView view = owner.GetComponentInChildren<LevelMenuView>(true);
                Assert.That(view, Is.Not.Null);
                LevelButtonView[] buttons = GetPrivateField<LevelButtonView[]>(view, "levelButtons");
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
                        isCleared: false,
                        isBusy: false));
                }

                view.Show(states, _ => { });
                view.Show(states, _ => { });

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

        private static ApplicationSystemGroup CreateApplicationSystemGroup(
            GameFlowSystem gameFlowSystem,
            ApplicationUISystem applicationUiSystem)
        {
            return new ApplicationSystemGroup(
                new FramePacingSystem(),
                new SafeAreaSystem(new SafeAreaViewStub()),
                applicationUiSystem,
                gameFlowSystem);
        }

        private static GameFlowSystem CreateGameFlowSystem(
            LevelCatalog catalog,
            SaveSystem saveSystem,
            ApplicationUISystem applicationUiSystem)
        {
            return new GameFlowSystem(
                new ApplicationBootFlow(catalog, saveSystem, applicationUiSystem),
                new LevelMenuFlow(catalog, saveSystem, applicationUiSystem),
                new LevelTransitionFlow(
                    new LevelSceneSystem(new ImmediateLevelSceneGateway()),
                    applicationUiSystem),
                new SaveRecoveryFlow(saveSystem, applicationUiSystem));
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

        private sealed class RecordingApplicationUIView : IApplicationUIView
        {
            private Exception nextShowLoadingException;

            public void ThrowOnNextShowLoading(Exception exception)
            {
                nextShowLoadingException = exception;
            }

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

        private sealed class ImmediateLevelSceneGateway : ILevelSceneGateway
        {
            public void LoadLevel(
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

            public void UnloadLevel(
                LevelSceneHandle handle,
                Action<LevelTransitionResult> completion)
            {
                completion(new LevelTransitionResult(
                    LevelTransitionStatus.Success,
                    handle.LevelNumber,
                    string.Empty));
            }
        }

        private sealed class SafeAreaViewStub : ISafeAreaView
        {
            public Rect SafeArea => new Rect(0f, 0f, 100f, 100f);
            public Vector2Int ScreenSize => new Vector2Int(100, 100);

            public void ApplyAnchors(Vector2 anchorMin, Vector2 anchorMax)
            {
            }
        }
    }
}
