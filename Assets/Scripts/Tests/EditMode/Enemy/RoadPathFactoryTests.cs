using System;
using UnityEditor;
using System.Reflection;
using NUnit.Framework;
using TowerDefense3D.GridPlacement;
using UnityEngine;

namespace TowerDefense3D.Enemies.Tests.EditMode
{
    public sealed class RoadPathFactoryTests
    {
        private BoardDefinition definition;

        [TearDown]
        public void TearDown()
        {
            if (definition != null)
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void Create_AuthoredExitAtJunction_UsesChosenBranch()
        {
            definition = CreateBoard(new[]
            {
                Cell(0, 1, BoardCellFlags.RoadSpawn, RoadExitDirection.East),
                Cell(1, 1, BoardCellFlags.Road, RoadExitDirection.North),
                Cell(2, 1, BoardCellFlags.Road),
                Cell(1, 2, BoardCellFlags.Road, RoadExitDirection.East),
                Cell(2, 2, BoardCellFlags.RoadEnd)
            });

            RoadPath path = RoadPathFactory.Create(new BoardSystem(new BoardViewStub(definition)));
            Vector3 position = path.Start;
            int targetPoint = 1;

            path.Move(ref targetPoint, ref position, 2f);

            Assert.That(path.PointCount, Is.EqualTo(4));
            Assert.That(position, Is.EqualTo(new Vector3(1.5f, 0f, 2.5f)));
        }

        [Test]
        public void Create_AuthoredRouteWithMissingExit_ThrowsClearError()
        {
            definition = CreateBoard(new[]
            {
                Cell(0, 0, BoardCellFlags.RoadSpawn, RoadExitDirection.East),
                Cell(1, 0, BoardCellFlags.Road),
                Cell(2, 0, BoardCellFlags.RoadEnd)
            });

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => RoadPathFactory.Create(new BoardSystem(new BoardViewStub(definition))));

            StringAssert.Contains("needs an authored exit direction", exception.Message);
        }

        [Test]
        public void CreatePaths_MultipleSpawns_RotatesWaveEnemiesAcrossFixedRoutes()
        {
            definition = CreateBoard(new[]
            {
                Cell(0, 0, BoardCellFlags.RoadSpawn, RoadExitDirection.East),
                Cell(1, 0, BoardCellFlags.Road, RoadExitDirection.East),
                Cell(2, 0, BoardCellFlags.RoadEnd),
                Cell(0, 2, BoardCellFlags.RoadSpawn, RoadExitDirection.East),
                Cell(1, 2, BoardCellFlags.Road, RoadExitDirection.East),
                Cell(2, 2, BoardCellFlags.RoadEnd)
            });

            RoadPathSet paths = RoadPathFactory.CreatePaths(
                new BoardSystem(new BoardViewStub(definition)));

            Assert.That(paths.Count, Is.EqualTo(2));
            Assert.That(paths.GetRouteIndex(1), Is.EqualTo(0));
            Assert.That(paths.GetRouteIndex(2), Is.EqualTo(1));
            Assert.That(paths.GetRouteIndex(3), Is.EqualTo(0));
            Assert.That(paths.Get(0).Start, Is.EqualTo(new Vector3(0.5f, 0f, 0.5f)));
            Assert.That(paths.Get(1).Start, Is.EqualTo(new Vector3(0.5f, 0f, 2.5f)));
        }

        [Test]
        public void RoadPathSet_SelectedRoadSpawn_OnlyChoosesItsWeightedRoutes()
        {
            var paths = new RoadPathSet(
                new[]
                {
                    new RoadPath(new[] { Vector3.zero, Vector3.right }),
                    new RoadPath(new[] { Vector3.zero, Vector3.forward }),
                    new RoadPath(new[] { Vector3.one, Vector3.one + Vector3.right })
                },
                new[] { 1, 3, 1 },
                new[] { 0, 0, 1 });

            Assert.That(paths.SpawnPointCount, Is.EqualTo(2));
            Assert.That(paths.GetRouteIndex(7L, 1), Is.EqualTo(2));
            Assert.That(paths.GetRouteIndex(7L, 0), Is.InRange(0, 1));
        }

        [Test]
        public void CreatePaths_SplitsEachRoadIntoLanesWithTheBossOnTheCentre()
        {
            definition = CreateBoard(new[]
            {
                Cell(0, 0, BoardCellFlags.RoadSpawn, RoadExitDirection.East),
                Cell(1, 0, BoardCellFlags.Road, RoadExitDirection.East),
                Cell(2, 0, BoardCellFlags.RoadEnd)
            });
            RoadPathSet paths = RoadPathFactory.CreatePaths(
                new BoardSystem(new BoardViewStub(definition)));
            EnemyDefinition regular = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(
                "Assets/Config/Enemies/Basic.asset");
            EnemyDefinition boss = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(
                "Assets/Config/Enemies/SummonerBoss.asset");

            {
                Assert.That(
                    paths.GetLaneIndex(7L, boss),
                    Is.EqualTo(RoadPathSet.CenterLaneIndex),
                    "Bosses must stay on the lane the road was drawn along.");
                Assert.That(
                    paths.GetLane(0, RoadPathSet.CenterLaneIndex).Start,
                    Is.EqualTo(paths.Get(0).Start));

                // The road runs east, so its lanes are separated along Z and only along Z.
                Vector3 left = paths.GetLane(0, 0).Start;
                Vector3 right = paths.GetLane(0, 2).Start;
                Assert.That(left.x, Is.EqualTo(right.x).Within(0.0001f));
                Assert.That(left.z, Is.Not.EqualTo(right.z));
                Assert.That(
                    paths.GetLaneIndex(7L, regular),
                    Is.InRange(0, RoadPathSet.LaneCount - 1));
            }
        }

        [Test]
        public void CreatePaths_AuthoredRouteLapsALoop_WalksTheLoopInsteadOfTheShortcut()
        {
            definition = CreateBoard(new[]
            {
                Cell(1, 0, BoardCellFlags.RoadSpawn, RoadExitDirection.North),
                Cell(1, 1, BoardCellFlags.Road, RoadExitDirection.East),
                Cell(0, 1, BoardCellFlags.Road),
                Cell(0, 2, BoardCellFlags.Road),
                Cell(1, 2, BoardCellFlags.Road),
                Cell(2, 1, BoardCellFlags.Road, RoadExitDirection.North),
                Cell(2, 2, BoardCellFlags.RoadEnd)
            });
            SetField(definition, "routes", new[]
            {
                new BoardRouteDefinition(new[]
                {
                    new GridCell(1, 0, 0),
                    new GridCell(1, 1, 0),
                    new GridCell(0, 1, 0),
                    new GridCell(0, 2, 0),
                    new GridCell(1, 2, 0),
                    new GridCell(1, 1, 0),
                    new GridCell(2, 1, 0),
                    new GridCell(2, 2, 0)
                })
            });

            RoadPath path = RoadPathFactory.Create(new BoardSystem(new BoardViewStub(definition)));

            Assert.That(path.PointCount, Is.EqualTo(8));
            Assert.That(path.Start, Is.EqualTo(new Vector3(1.5f, 0f, 0.5f)));
            Assert.That(path.End, Is.EqualTo(new Vector3(2.5f, 0f, 2.5f)));
        }

        [Test]
        public void CreatePaths_EmptyAuthoredRoute_FallsBackToTheExitArrows()
        {
            definition = CreateBoard(new[]
            {
                Cell(0, 0, BoardCellFlags.RoadSpawn, RoadExitDirection.East),
                Cell(1, 0, BoardCellFlags.Road, RoadExitDirection.East),
                Cell(2, 0, BoardCellFlags.RoadEnd)
            });
            // The Board Painter creates a route before anything is drawn into it. That must read
            // as "not authored yet" and leave the board on its arrows, not stop the level loading.
            SetField(definition, "routes", new[] { new BoardRouteDefinition(new GridCell[0]) });

            RoadPath path = RoadPathFactory.Create(new BoardSystem(new BoardViewStub(definition)));

            Assert.That(path.PointCount, Is.EqualTo(3));
            Assert.That(path.Start, Is.EqualTo(new Vector3(0.5f, 0f, 0.5f)));
        }

        [Test]
        public void CreatePaths_AuthoredRouteSkipsACell_ThrowsClearError()
        {
            definition = CreateBoard(new[]
            {
                Cell(0, 0, BoardCellFlags.RoadSpawn, RoadExitDirection.East),
                Cell(1, 0, BoardCellFlags.Road, RoadExitDirection.East),
                Cell(2, 0, BoardCellFlags.RoadEnd)
            });
            SetField(definition, "routes", new[]
            {
                new BoardRouteDefinition(new[]
                {
                    new GridCell(0, 0, 0),
                    new GridCell(2, 0, 0)
                })
            });

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => RoadPathFactory.Create(new BoardSystem(new BoardViewStub(definition))));

            StringAssert.Contains("do not share an edge", exception.Message);
        }

        private static BoardCellDefinition Cell(
            int x,
            int z,
            BoardCellFlags flags,
            RoadExitDirection direction = RoadExitDirection.None) =>
            new BoardCellDefinition(new GridCell(x, z, 0), flags, direction);

        private static BoardDefinition CreateBoard(BoardCellDefinition[] cells)
        {
            BoardDefinition board = ScriptableObject.CreateInstance<BoardDefinition>();
            SetField(board, "dimensions", new GridDimensions(3, 3, 1));
            SetField(board, "cellSize", 1f);
            SetField(board, "heightUnit", 1f);
            SetField(board, "cells", cells);
            return board;
        }

        private static void SetField<T>(BoardDefinition target, string name, T value)
        {
            FieldInfo field = typeof(BoardDefinition).GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Expected serialized field '{name}'.");
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

            public void ApplyVisibility(bool visible)
            {
                _ = visible;
            }
        }
    }
}
