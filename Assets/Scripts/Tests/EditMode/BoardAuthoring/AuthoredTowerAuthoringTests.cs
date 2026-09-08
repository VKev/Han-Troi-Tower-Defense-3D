using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TowerDefense3D.GridPlacement.Editor;
using TowerDefense3D.Towers;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.GridPlacement.Tests.EditMode
{
    public sealed class AuthoredTowerAuthoringTests
    {
        private const string FireDefinitionPath =
            "Assets/Config/Towers/Definitions/Elements/Fire.asset";

        private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
        private TowerCombatDefinition fire;

        [SetUp]
        public void SetUp()
        {
            fire = AssetDatabase.LoadAssetAtPath<TowerCombatDefinition>(FireDefinitionPath);
            Assert.That(fire, Is.Not.Null);
        }

        [TearDown]
        public void TearDown()
        {
            for (int index = created.Count - 1; index >= 0; index--)
            {
                if (created[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(created[index]);
                }
            }

            created.Clear();
        }

        [Test]
        public void Document_PersistsTowerAndRejectsOverlappingFootprint()
        {
            BoardDefinition board = CreateBoard();
            var document = new BoardAuthoringDocument(board);
            var anchor = new GridCell(2, 2, 0);

            Assert.That(document.TrySetAuthoredTower(anchor, fire, out string error), Is.True, error);
            Assert.That(
                document.TrySetAuthoredTower(new GridCell(3, 2, 0), fire, out error),
                Is.False);
            StringAssert.Contains("overlaps", error);
            Assert.That(
                document.TrySetAuthoredTower(new GridCell(5, 5, 0), fire, out error),
                Is.False);
            StringAssert.Contains("outside", error);

            GameObject roadPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Prefabs/RoadStraightCell.prefab");
            Assert.That(roadPrefab, Is.Not.Null);
            Assert.Throws<ArgumentException>(() =>
                document.SetGridPlaceable(new GridCell(3, 3, 0), roadPrefab));

            document.Commit("Test authored tower persistence");
            var reloaded = new BoardAuthoringDocument(board);

            Assert.That(reloaded.ActiveAuthoredTowerCount, Is.EqualTo(1));
            Assert.That(reloaded.GetAuthoredTower(anchor), Is.SameAs(fire));
            Assert.That(
                reloaded.TryGetAuthoredTowerAtCell(
                    new GridCell(3, 3, 0), out GridCell foundAnchor, out TowerCombatDefinition found),
                Is.True);
            Assert.That(foundAnchor, Is.EqualTo(anchor));
            Assert.That(found, Is.SameAs(fire));
        }

        [Test]
        public void Synchronizer_CreatesDynamicTowerAndReusesMatchingInstance()
        {
            BoardDefinition board = CreateBoard();
            var anchor = new GridCell(2, 2, 0);
            SetField(board, "authoredTowers", new[] { new AuthoredTowerPlacement(anchor, fire) });
            var presenterObject = Track(new GameObject("Authored Tower Board"));
            BoardView presenter = presenterObject.AddComponent<BoardView>();
            SetField(presenter, "board", board);

            BoardSceneSynchronizer.Synchronize(board);

            Transform root = presenter.GeneratedAuthoredTowerRoot;
            Assert.That(root, Is.Not.Null);
            Assert.That(root.name, Is.EqualTo(BoardSceneSynchronizer.GeneratedAuthoredTowerRootName));
            Assert.That(root.childCount, Is.EqualTo(1));
            Transform tower = root.GetChild(0);
            Assert.That(
                PrefabUtility.GetCorrespondingObjectFromSource(tower.gameObject),
                Is.SameAs(fire.Core.PlacementDefinition.Prefab));
            Assert.That(tower.GetComponent<TowerRuntimeView>(), Is.Not.Null);
            Assert.That(tower.GetComponent<AuthoredTowerView>().Definition, Is.SameAs(fire));
            foreach (Transform child in tower.GetComponentsInChildren<Transform>(true))
            {
                Assert.That(child.gameObject.isStatic, Is.False, child.name);
            }

            var mapper = new GridCoordinateMapper(board.Dimensions, board.CellSize, board.HeightUnit, Vector3.zero);
            Vector3 expected = mapper.FootprintBottomCenter(anchor, fire.Core.PlacementDefinition.Footprint);
            Assert.That(Vector3.Distance(tower.localPosition, expected), Is.LessThan(0.0001f));

            int instanceId = tower.gameObject.GetInstanceID();
            BoardSceneSynchronizer.Synchronize(board);
            Assert.That(root.GetChild(0).gameObject.GetInstanceID(), Is.EqualTo(instanceId));
        }

        private BoardDefinition CreateBoard()
        {
            BoardDefinition board = Track(ScriptableObject.CreateInstance<BoardDefinition>());
            var dimensions = new GridDimensions(6, 6, 2);
            var cells = new List<BoardCellDefinition>();
            for (int z = 0; z < dimensions.Depth; z++)
            {
                for (int x = 0; x < dimensions.Width; x++)
                {
                    cells.Add(new BoardCellDefinition(
                        new GridCell(x, z, 0),
                        BoardCellFlags.SupportsPlacement | BoardCellFlags.Buildable));
                    cells.Add(new BoardCellDefinition(
                        new GridCell(x, z, 1),
                        BoardCellFlags.None));
                }
            }

            SetField(board, "dimensions", dimensions);
            SetField(board, "cellSize", 1f);
            SetField(board, "heightUnit", 1f);
            SetField(board, "cells", cells.ToArray());
            return board;
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            created.Add(value);
            return value;
        }

        private static void SetField<T>(UnityEngine.Object target, string name, T value)
        {
            FieldInfo field = target.GetType().GetField(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing serialized field '{name}'.");
            field.SetValue(target, value);
        }
    }
}
