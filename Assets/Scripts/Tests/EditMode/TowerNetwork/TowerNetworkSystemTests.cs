using System.Reflection;
using NUnit.Framework;
using TowerDefense3D.GameplayInput;
using TowerDefense3D.GridPlacement;
using TowerDefense3D.Towers;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.GameFlow.Tests.EditMode
{
    public sealed class TowerNetworkSystemTests
    {
        private const string BoardPath = "Assets/Config/GridPlacement/Level_001_Board.asset";
        private const string TowerCatalogPath = "Assets/Config/Towers/Catalogs/TowerCatalog.asset";

        private GameObject owner;
        private TowerCatalog towerCatalog;
        private TowerNetworkManager manager;
        private TowerNetworkSystem system;

        [SetUp]
        public void SetUp()
        {
            BoardDefinition board = AssetDatabase.LoadAssetAtPath<BoardDefinition>(BoardPath);
            towerCatalog = AssetDatabase.LoadAssetAtPath<TowerCatalog>(TowerCatalogPath);
            Assert.That(board, Is.Not.Null, $"Board is missing at '{BoardPath}'.");
            Assert.That(towerCatalog, Is.Not.Null, $"Tower Catalog is missing at '{TowerCatalogPath}'.");

            var boardSystem = new BoardSystem(new BoardViewStub(board));
            var inputSystem = new GameplayInputSystem(new GameplayInputSourceStub());
            var placementSystem = new GridPlacementSystem(
                boardSystem,
                inputSystem,
                new GridPlacementViewStub(),
                new TowerInstanceFactoryStub());

            manager = new TowerNetworkManager(towerCatalog);
            system = new TowerNetworkSystem(manager, placementSystem, 1);
            owner = new GameObject("Tower Network System Test");
        }

        [TearDown]
        public void TearDown()
        {
            system?.Dispose();
            if (owner != null)
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void StartAndDispose_OwnExactlyOneLevelSession()
        {
            system.Start();

            Assert.That(manager.HasLevelSession, Is.True);
            Assert.That(manager.ActiveLevelNumber, Is.EqualTo(1));

            system.Dispose();

            Assert.That(manager.HasLevelSession, Is.False);
        }

        [Test]
        public void PlacementRegistration_PreservesCombatDefinitionAndBindsRuntimeNode()
        {
            system.Start();
            TowerRuntimeView generatorView = RegisterTower(TowerFamily.Generator, Vector3.zero, 1);

            Assert.That(system.RegisteredTowerCount, Is.EqualTo(1));
            Assert.That(generatorView.IsRegistered, Is.True);
            Assert.That(manager.TryGetNodeSpec(generatorView.NodeId, out TowerRuntimeSpec spec), Is.True);
            Assert.That(spec.Family, Is.EqualTo(TowerFamily.Generator));
            Assert.That(system.TryGetTowerView(generatorView.NodeId, out ITowerRuntimeView registered), Is.True);
            Assert.That(registered, Is.SameAs(generatorView));
        }

        [Test]
        public void Dispose_ClearsRuntimeViewRegistryAndNodeBinding()
        {
            system.Start();
            TowerRuntimeView generatorView = RegisterTower(TowerFamily.Generator, Vector3.zero, 1);

            system.Dispose();

            Assert.That(system.RegisteredTowerCount, Is.Zero);
            Assert.That(system.CreateTowerViewSnapshot(), Is.Empty);
            Assert.That(generatorView.IsRegistered, Is.False);
            Assert.That(manager.NodeCount, Is.Zero);
        }

        [Test]
        public void RegisteredGeneratorAndNexus_CanLinkUnlinkAndGateStartWave()
        {
            system.Start();
            TowerRuntimeView generatorView = RegisterTower(TowerFamily.Generator, Vector3.zero, 1);
            TowerRuntimeView nexusView = RegisterTower(TowerFamily.SoulNexus, Vector3.right, 2);

            Assert.That(system.TryStartSimulation(out string missingChainError), Is.False);
            StringAssert.Contains("valid Generator", missingChainError);
            Assert.That(system.TryRewire(generatorView, nexusView, out string linkError), Is.True, linkError);

            system.Select(generatorView);
            Assert.That(system.TryUnlinkSelected(out string unlinkError), Is.True, unlinkError);
            Assert.That(manager.LinkCount, Is.Zero);

            Assert.That(system.TryRewire(generatorView, nexusView, out linkError), Is.True, linkError);
            Assert.That(system.TryStartSimulation(out string startError), Is.True, startError);
            Assert.That(system.IsRunning, Is.True);
        }

        [Test]
        public void DestroyedRuntimeView_UnregistersItsNodeAndLinks()
        {
            system.Start();
            TowerRuntimeView generatorView = RegisterTower(TowerFamily.Generator, Vector3.zero, 1);
            TowerRuntimeView nexusView = RegisterTower(TowerFamily.SoulNexus, Vector3.right, 2);
            Assert.That(system.TryRewire(generatorView, nexusView, out string linkError), Is.True, linkError);

            InvokePrivate(generatorView, "OnDestroy");
            Object.DestroyImmediate(generatorView.gameObject);

            Assert.That(system.RegisteredTowerCount, Is.EqualTo(1));
            Assert.That(manager.NodeCount, Is.EqualTo(1));
            Assert.That(manager.LinkCount, Is.Zero);
            Assert.That(manager.HasValidChain, Is.False);
        }

        private TowerRuntimeView RegisterTower(TowerFamily family, Vector3 position, int ownerId)
        {
            Assert.That(towerCatalog.TryGet(family, out TowerCombatDefinition definition), Is.True);
            var towerObject = new GameObject(family + " Test Tower");
            towerObject.transform.SetParent(owner.transform, false);
            towerObject.transform.position = position;
            TowerRuntimeView view = towerObject.AddComponent<TowerRuntimeView>();

            SetPrivateField(system, "placementCombatDefinition", definition);
            InvokePrivate(
                system,
                "HandleTowerPlaced",
                new GridPlacementCommit(definition.Core.PlacementDefinition, towerObject, default, ownerId));
            return view;
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
            Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}'.");
            field.SetValue(target, value);
        }

        private sealed class BoardViewStub : IBoardView
        {
            public BoardViewStub(BoardDefinition board)
            {
                Board = board;
            }

            public BoardDefinition Board { get; }
            public Vector3 WorldOrigin => Vector3.zero;
            public void ApplyVisibility(bool visible) => _ = visible;
        }

        private sealed class GameplayInputSourceStub : IGameplayInputSource
        {
            public GameplayInputSnapshot Capture() => default;
        }

        private sealed class GridPlacementViewStub : IGridPlacementView
        {
            public bool TryGetWorldPoint(Vector2 screenPosition, out Vector3 worldPoint)
            {
                _ = screenPosition;
                worldPoint = default;
                return false;
            }

            public void Show(
                TowerFootprint footprint,
                Vector3 footprintBottomCenter,
                float cellSize,
                float heightUnit,
                bool isValid)
            {
                _ = footprint;
                _ = footprintBottomCenter;
                _ = cellSize;
                _ = heightUnit;
                _ = isValid;
            }

            public void Hide()
            {
            }
        }

        private sealed class TowerInstanceFactoryStub : ITowerInstanceFactory
        {
            public bool TryCreate(TowerDefinition definition, Vector3 position, out GameObject instance)
            {
                _ = definition;
                _ = position;
                instance = null;
                return false;
            }

            public void Destroy(GameObject instance) => Object.DestroyImmediate(instance);
        }
    }
}
