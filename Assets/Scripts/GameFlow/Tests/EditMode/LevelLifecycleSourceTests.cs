using System;
using NUnit.Framework;
using TowerDefense3D.GameplayInput;
using TowerDefense3D.GridPlacement;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.GameFlow.Tests.EditMode
{
    public sealed class LevelLifecycleSourceTests
    {
        private const string PlacementPresenterSourcePath =
            "Assets/Scripts/Placement/GridPlacementPresenter.cs";

        [Test]
        public void PlacementPresenter_HasNoUnityFrameLifecycleAndDelegatesToSystem()
        {
            MonoScript sourceAsset = AssetDatabase.LoadAssetAtPath<MonoScript>(
                PlacementPresenterSourcePath);

            Assert.That(sourceAsset, Is.Not.Null);
            string source = sourceAsset.text;
            StringAssert.DoesNotContain("void Awake(", source);
            StringAssert.DoesNotContain("void Start(", source);
            StringAssert.DoesNotContain("void Update(", source);
            StringAssert.Contains("GridPlacementSystem", source);
        }

        [Test]
        public void PlacementPresenter_BindsDirectlyAndSupportsRebindAfterShutdown()
        {
            GameObject owner = new GameObject("Placement Lifecycle Test");
            BoardDefinition boardDefinition = ScriptableObject.CreateInstance<BoardDefinition>();
            GridPlacementPresenter presenter = owner.AddComponent<GridPlacementPresenter>();
            GridPlacementView view = owner.AddComponent<GridPlacementView>();
            GridPlacementSystem system = CreatePlacementSystem(boardDefinition, view);

            try
            {
                Assert.That(presenter.IsInitialized, Is.False);
                Assert.Throws<InvalidOperationException>(
                    () => presenter.SelectTower((TowerDefinition)null));

                presenter.Bind(system, view);

                Assert.That(presenter.IsInitialized, Is.True);
                Assert.That(presenter.Occupancy, Is.SameAs(system.Occupancy));

                presenter.Shutdown();
                presenter.Shutdown();

                Assert.That(presenter.IsInitialized, Is.False);
                Assert.That(presenter.Occupancy, Is.Null);

                presenter.Bind(system, view);
                Assert.That(presenter.IsInitialized, Is.True);
            }
            finally
            {
                presenter.Shutdown();
                UnityEngine.Object.DestroyImmediate(boardDefinition);
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        private static GridPlacementSystem CreatePlacementSystem(
            BoardDefinition boardDefinition,
            GridPlacementView view)
        {
            var boardSystem = new BoardSystem(new StubBoardView(boardDefinition));
            var inputSystem = new GameplayInputSystem(new StubGameplayInputSource());
            return new GridPlacementSystem(
                boardSystem,
                inputSystem,
                view,
                new StubTowerInstanceFactory());
        }

        private sealed class StubBoardView : IBoardView
        {
            public StubBoardView(BoardDefinition board)
            {
                Board = board;
            }

            public BoardDefinition Board { get; }
            public Vector3 WorldOrigin => Vector3.zero;

            public void ApplyVisibility(bool visible)
            {
            }
        }

        private sealed class StubGameplayInputSource : IGameplayInputSource
        {
            public GameplayInputSnapshot Capture()
            {
                return default;
            }
        }

        private sealed class StubTowerInstanceFactory : ITowerInstanceFactory
        {
            public bool TryCreate(
                TowerDefinition definition,
                Vector3 position,
                out GameObject instance)
            {
                instance = null;
                return false;
            }

            public void Destroy(GameObject instance)
            {
            }
        }
    }
}
