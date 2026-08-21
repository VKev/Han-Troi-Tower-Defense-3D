using System;
using System.Reflection;
using NUnit.Framework;
using TowerDefense3D.GridPlacement;
using TowerDefense3D.Towers;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.GameFlow.Tests.EditMode
{
    public sealed class TowerNetworkSceneAdapterTests
    {
        private const string TowerCatalogPath = "Assets/Config/Towers/Catalogs/TowerCatalog.asset";

        private GameObject owner;
        private GameObject cameraOwner;
        private TowerCatalog towerCatalog;
        private TowerNetworkManager manager;
        private GridPlacementController placementController;
        private TowerSimulationDriver simulationDriver;
        private TowerNetworkInputController inputController;
        private TowerLinkPresenter linkPresenter;
        private TowerProjectilePresenter projectilePresenter;
        private TowerNetworkSceneAdapter adapter;

        [SetUp]
        public void SetUp()
        {
            towerCatalog = AssetDatabase.LoadAssetAtPath<TowerCatalog>(TowerCatalogPath);
            Assert.That(towerCatalog, Is.Not.Null, $"Tower Catalog is missing at '{TowerCatalogPath}'.");

            manager = new TowerNetworkManager(towerCatalog);
            owner = new GameObject("Tower Network Scene Adapter Test");
            cameraOwner = new GameObject("Tower Network Camera Test");
            Camera worldCamera = cameraOwner.AddComponent<Camera>();
            placementController = owner.AddComponent<GridPlacementController>();
            simulationDriver = owner.AddComponent<TowerSimulationDriver>();
            inputController = owner.AddComponent<TowerNetworkInputController>();
            linkPresenter = owner.AddComponent<TowerLinkPresenter>();
            projectilePresenter = owner.AddComponent<TowerProjectilePresenter>();
            adapter = owner.AddComponent<TowerNetworkSceneAdapter>();

            SetPrivateField(placementController, "worldCamera", worldCamera);
        }

        [TearDown]
        public void TearDown()
        {
            if (owner != null)
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }

            if (cameraOwner != null)
            {
                UnityEngine.Object.DestroyImmediate(cameraOwner);
            }
        }

        [Test]
        public void InitializeAndShutdown_OwnExactlyOneLevelSessionAndEverySceneLeaf()
        {
            adapter.Initialize(CreateContext(2));

            Assert.That(adapter.IsInitialized, Is.True);
            Assert.That(simulationDriver.IsInitialized, Is.True);
            Assert.That(inputController.IsInitialized, Is.True);
            Assert.That(linkPresenter.IsInitialized, Is.True);
            Assert.That(projectilePresenter.IsInitialized, Is.True);
            Assert.That(manager.HasLevelSession, Is.True);
            Assert.That(manager.ActiveLevelNumber, Is.EqualTo(2));

            adapter.Shutdown();

            Assert.That(adapter.IsInitialized, Is.False);
            Assert.That(simulationDriver.IsInitialized, Is.False);
            Assert.That(inputController.IsInitialized, Is.False);
            Assert.That(linkPresenter.IsInitialized, Is.False);
            Assert.That(projectilePresenter.IsInitialized, Is.False);
            Assert.That(manager.HasLevelSession, Is.False);
        }

        [Test]
        public void Initialize_WithoutTowerManager_ThrowsWithoutStartingSceneLeaves()
        {
            var context = new LevelSceneRuntimeContext(1, () => { });

            Assert.Throws<InvalidOperationException>(() => adapter.Initialize(context));

            Assert.That(adapter.IsInitialized, Is.False);
            Assert.That(simulationDriver.IsInitialized, Is.False);
            Assert.That(inputController.IsInitialized, Is.False);
            Assert.That(manager.HasLevelSession, Is.False);
        }

        [Test]
        public void Initialize_WithoutRequiredPresenter_DoesNotLeakSession()
        {
            UnityEngine.Object.DestroyImmediate(projectilePresenter);
            projectilePresenter = null;

            Assert.Throws<InvalidOperationException>(() => adapter.Initialize(CreateContext(1)));

            Assert.That(adapter.IsInitialized, Is.False);
            Assert.That(manager.HasLevelSession, Is.False);
        }

        [Test]
        public void PlacementRegistration_PreservesCombatDefinitionAndBindsRuntimeNode()
        {
            adapter.Initialize(CreateContext(1));
            TowerRuntimeView generatorView = CreateRuntimeView(TowerFamily.Generator, new Vector3(1f, 0f, 2f));

            RegisterPlacement(generatorView, 1);

            Assert.That(adapter.RegisteredTowerCount, Is.EqualTo(1));
            Assert.That(generatorView.IsRegistered, Is.True);
            Assert.That(generatorView.CombatDefinition.Family, Is.EqualTo(TowerFamily.Generator));
            Assert.That(manager.TryGetNodeSpec(generatorView.NodeId, out TowerRuntimeSpec spec), Is.True);
            Assert.That(spec.Family, Is.EqualTo(TowerFamily.Generator));
            Assert.That(adapter.TryGetTowerView(generatorView.NodeId, out TowerRuntimeView registered), Is.True);
            Assert.That(registered, Is.SameAs(generatorView));
        }

        [Test]
        public void RegisteredGeneratorAndNexus_CanLinkUnlinkAndGateStartWave()
        {
            adapter.Initialize(CreateContext(1));
            TowerRuntimeView generatorView = CreateRuntimeView(TowerFamily.Generator, Vector3.zero);
            TowerRuntimeView nexusView = CreateRuntimeView(TowerFamily.SoulNexus, Vector3.right);
            RegisterPlacement(generatorView, 1);
            RegisterPlacement(nexusView, 2);

            Assert.That(adapter.TryStartSimulation(out string missingChainError), Is.False);
            StringAssert.Contains("valid Generator", missingChainError);
            Assert.That(adapter.TryRewire(generatorView, nexusView, out string linkError), Is.True, linkError);
            Assert.That(adapter.HasValidChain, Is.True);

            inputController.Select(generatorView);
            Assert.That(adapter.TryUnlinkSelected(out string unlinkError), Is.True, unlinkError);
            Assert.That(adapter.HasValidChain, Is.False);
            Assert.That(manager.LinkCount, Is.Zero);

            Assert.That(adapter.TryRewire(generatorView, nexusView, out linkError), Is.True, linkError);
            Assert.That(adapter.TryStartSimulation(out string startError), Is.True, startError);
            Assert.That(adapter.IsRunning, Is.True);
        }

        [Test]
        public void DestroyedRuntimeView_UnregistersItsNodeAndLinks()
        {
            adapter.Initialize(CreateContext(1));
            TowerRuntimeView generatorView = CreateRuntimeView(TowerFamily.Generator, Vector3.zero);
            TowerRuntimeView nexusView = CreateRuntimeView(TowerFamily.SoulNexus, Vector3.right);
            RegisterPlacement(generatorView, 1);
            RegisterPlacement(nexusView, 2);
            Assert.That(adapter.TryRewire(generatorView, nexusView, out string linkError), Is.True, linkError);

            InvokePrivate(generatorView, "OnDestroy");
            UnityEngine.Object.DestroyImmediate(generatorView.gameObject);

            Assert.That(adapter.RegisteredTowerCount, Is.EqualTo(1));
            Assert.That(manager.NodeCount, Is.EqualTo(1));
            Assert.That(manager.LinkCount, Is.Zero);
            Assert.That(manager.HasValidChain, Is.False);
        }

        [Test]
        public void OnDestroy_EndsTheOwnedLevelSession()
        {
            adapter.Initialize(CreateContext(1));
            Assert.That(manager.HasLevelSession, Is.True);

            InvokePrivate(adapter, "OnDestroy");

            Assert.That(manager.HasLevelSession, Is.False);
        }

        private LevelSceneRuntimeContext CreateContext(int levelNumber)
        {
            return new LevelSceneRuntimeContext(levelNumber, () => { }, manager);
        }

        private TowerRuntimeView CreateRuntimeView(TowerFamily family, Vector3 position)
        {
            Assert.That(towerCatalog.TryGet(family, out TowerCombatDefinition definition), Is.True);
            var towerObject = new GameObject(family + " Test Tower");
            towerObject.transform.SetParent(owner.transform, false);
            towerObject.transform.position = position;
            TowerRuntimeView view = towerObject.AddComponent<TowerRuntimeView>();
            view.Configure(definition);
            return view;
        }

        private void RegisterPlacement(TowerRuntimeView view, int ownerId)
        {
            var placement = new TowerPlacementRecord(
                view.CombatDefinition,
                view.CombatDefinition.Core.PlacementDefinition,
                view,
                new GridCell(ownerId, 0, 0),
                ownerId);
            InvokePrivate(adapter, "HandleTowerPlaced", placement);
        }

        private static void InvokePrivate(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Missing private method '{methodName}'.");
            method.Invoke(target, arguments);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}'.");
            field.SetValue(target, value);
        }
    }
}
