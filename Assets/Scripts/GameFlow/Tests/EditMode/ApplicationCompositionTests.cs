using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace TowerDefense3D.GameFlow.Tests.EditMode
{
    public sealed class ApplicationCompositionTests
    {
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
            var applicationStartables = new List<Type>();
            Type[] runtimeTypes = typeof(GameFlowCoordinator).Assembly.GetTypes();
            for (int index = 0; index < runtimeTypes.Length; index++)
            {
                if (ImplementsInterface(runtimeTypes[index], "VContainer.Unity.IStartable"))
                {
                    applicationStartables.Add(runtimeTypes[index]);
                }
            }

            Assert.That(
                typeof(ApplicationLifetimeScope).BaseType?.FullName,
                Is.EqualTo("VContainer.Unity.LifetimeScope"));
            Assert.That(typeof(MonoBehaviour).IsAssignableFrom(typeof(GameFlowCoordinator)), Is.False);
            CollectionAssert.AreEqual(new[] { typeof(GameFlowCoordinator) }, applicationStartables);
            Assert.That(typeof(IDisposable).IsAssignableFrom(typeof(GameFlowCoordinator)), Is.True);
            Assert.That(typeof(MonoBehaviour).IsAssignableFrom(typeof(SaveCoordinator)), Is.False);
        }

        [Test]
        public void GameFlowCoordinator_StartAndDispose_ControlApplicationUiOnce()
        {
            LevelCatalog catalog = ScriptableObject.CreateInstance<LevelCatalog>();
            GameObject levelLoaderOwner = new GameObject("Level Loader Test");
            LevelSceneLoader levelSceneLoader = levelLoaderOwner.AddComponent<LevelSceneLoader>();
            var saveCoordinator = new SaveCoordinator(new LocalSaveRepository(testRoot), "test");
            var applicationUi = new RecordingApplicationUI();
            var coordinator = new GameFlowCoordinator(
                catalog,
                saveCoordinator,
                levelSceneLoader,
                applicationUi);

            try
            {
                coordinator.Start();
                coordinator.Start();

                Assert.That(applicationUi.InitializeCount, Is.EqualTo(1));
                Assert.That(coordinator.State, Is.EqualTo(GameFlowState.BlockingError));

                coordinator.Dispose();
                coordinator.Dispose();

                Assert.That(applicationUi.ShutdownCount, Is.EqualTo(1));
            }
            finally
            {
                coordinator.Dispose();
                UnityEngine.Object.DestroyImmediate(levelLoaderOwner);
                UnityEngine.Object.DestroyImmediate(catalog);
            }
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
            var coordinator = new GameFlowCoordinator(
                catalog,
                saveCoordinator,
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

                coordinator.Dispose();
                coordinator.Dispose();
                Assert.That(applicationUi.ShutdownCount, Is.EqualTo(2));
            }
            finally
            {
                coordinator.Dispose();
                UnityEngine.Object.DestroyImmediate(levelLoaderOwner);
                UnityEngine.Object.DestroyImmediate(catalog);
            }
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
            GameObject owner = new GameObject("Application UI Test");
            ApplicationUIManager manager = owner.AddComponent<ApplicationUIManager>();

            try
            {
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
                UnityEngine.Object.DestroyImmediate(owner);
            }
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
