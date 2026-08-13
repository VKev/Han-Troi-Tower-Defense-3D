using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TowerDefense3D.GridPlacement.Editor;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.GridPlacement.Tests.EditMode
{
    public sealed class BoardSceneAuthoringTests
    {
        private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
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
        public void Planner_MergesDuplicatesIgnoresBoundsAndOrdersDeterministically()
        {
            GridCell mixed = new GridCell(0, 0, 0);
            BoardDefinition board = CreateBoard(
                new GridDimensions(3, 2, 2),
                true,
                new[]
                {
                    new BoardCellDefinition(mixed, BoardCellFlags.SupportsPlacement),
                    new BoardCellDefinition(mixed, BoardCellFlags.StaticBlocker),
                    new BoardCellDefinition(new GridCell(1, 0, 0), BoardCellFlags.SupportsPlacement),
                    new BoardCellDefinition(new GridCell(2, 1, 1), BoardCellFlags.StaticBlocker),
                    new BoardCellDefinition(new GridCell(3, 0, 0), BoardCellFlags.SupportsPlacement),
                });

            BoardGeometryPlan first = BoardGeometryPlanner.Create(board);
            BoardGeometryPlan second = BoardGeometryPlanner.Create(board);

            Assert.That(first.Rectangles, Has.Count.EqualTo(3));
            AssertRectangle(first.Rectangles[0], BoardGeometryKind.PlacementSurface, 0, 0, 0, 2, 1);
            AssertRectangle(first.Rectangles[1], BoardGeometryKind.StaticBlocker, 0, 0, 0, 1, 1);
            AssertRectangle(first.Rectangles[2], BoardGeometryKind.StaticBlocker, 2, 1, 1, 1, 1);
            Assert.That(second.Signature, Is.EqualTo(first.Signature));
        }

        [Test]
        public void Synchronizer_OwnsGeneratedGeometryAlignsAndReusesUntilBoardChanges()
        {
            const float heightUnit = 2f;
            BoardDefinition board = CreateBoard(
                new GridDimensions(2, 2, 2),
                false,
                new[]
                {
                    new BoardCellDefinition(
                        new GridCell(0, 0, 1),
                        BoardCellFlags.SupportsPlacement),
                    new BoardCellDefinition(
                        new GridCell(1, 0, 1),
                        BoardCellFlags.StaticBlocker),
                },
                heightUnit);
            GameObject presenterObject = Track(new GameObject("Board Presenter"));
            BoardScenePresenter presenter = presenterObject.AddComponent<BoardScenePresenter>();
            SetField(presenter, "board", board);
            GameObject manual = new GameObject("Manual Child");
            manual.transform.SetParent(presenterObject.transform, false);

            BoardSceneSynchronizer.Synchronize(board);

            Transform root = presenterObject.transform.Find(BoardSceneSynchronizer.GeneratedRootName);
            Assert.That(root, Is.Not.Null);
            Assert.That(presenter.GeneratedRoot, Is.SameAs(root));
            Assert.That(manual.transform.parent, Is.SameAs(presenterObject.transform));
            Transform surface = root.Find(
                BoardSceneSynchronizer.PlaceableAreaName);
            Transform blocker = root.Find(
                BoardSceneSynchronizer.BlockedAreaName);
            Assert.That(surface, Is.Not.Null);
            Assert.That(blocker, Is.Not.Null);
            AssertGeneratedNamesAreReadable(root);
            Assert.That(GetField<string>(presenter, "generatedSignature"), Is.Not.Empty);
            Assert.That(surface.GetComponent<MeshRenderer>().enabled, Is.False);
            Assert.That(blocker.GetComponent<MeshRenderer>().enabled, Is.False);
            Assert.That(surface.GetComponent<BoxCollider>().enabled, Is.True);
            Assert.That(blocker.GetComponent<BoxCollider>().enabled, Is.True);
            Physics.SyncTransforms();
            Assert.That(surface.GetComponent<BoxCollider>().bounds.max.y, Is.EqualTo(heightUnit).Within(0.0001f));
            Assert.That(blocker.GetComponent<BoxCollider>().bounds.min.y, Is.EqualTo(heightUnit).Within(0.0001f));

            int childCount = root.childCount;
            int surfaceInstance = surface.gameObject.GetInstanceID();
            BoardSceneSynchronizer.Synchronize(board);
            Assert.That(root.childCount, Is.EqualTo(childCount));
            Assert.That(root.GetChild(0).gameObject.GetInstanceID(), Is.EqualTo(surfaceInstance));

            SetField(
                board,
                "cells",
                new[]
                {
                    new BoardCellDefinition(
                        new GridCell(1, 1, 0),
                        BoardCellFlags.SupportsPlacement),
                });
            BoardSceneSynchronizer.Synchronize(board);

            Assert.That(surface == null, Is.True);
            Assert.That(blocker == null, Is.True);
            Assert.That(root.Find(BoardSceneSynchronizer.BlockedAreaName), Is.Null);
            Transform replacement = root.Find(
                BoardSceneSynchronizer.PlaceableAreaName);
            Assert.That(replacement, Is.Not.Null);
            Assert.That(replacement.localPosition.x, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(replacement.localPosition.z, Is.EqualTo(1.5f).Within(0.0001f));
            AssertGeneratedNamesAreReadable(root);
            Assert.That(manual.transform.parent, Is.SameAs(presenterObject.transform));
        }

        private static void AssertGeneratedNamesAreReadable(Transform root)
        {
            Transform[] generated = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < generated.Length; index++)
            {
                string objectName = generated[index].name;
                Assert.That(objectName, Does.Not.Match(@"\d"));
                Assert.That(objectName, Does.Not.Contain("__"));
                Assert.That(objectName, Does.Not.Contain("Signature"));
            }
        }

        private BoardDefinition CreateBoard(
            GridDimensions dimensions,
            bool visualize,
            BoardCellDefinition[] cells,
            float heightUnit = 1f)
        {
            BoardDefinition board = Track(ScriptableObject.CreateInstance<BoardDefinition>());
            SetField(board, "dimensions", dimensions);
            SetField(board, "cellSize", 1f);
            SetField(board, "heightUnit", heightUnit);
            SetField(board, "visualizeInScene", visualize);
            SetField(board, "cells", cells);
            return board;
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            created.Add(value);
            return value;
        }

        private static void SetField<T>(UnityEngine.Object target, string fieldName, T value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Expected serialized field '{fieldName}'.");
            field.SetValue(target, value);
        }

        private static T GetField<T>(UnityEngine.Object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Expected serialized field '{fieldName}'.");
            return (T)field.GetValue(target);
        }

        private static void AssertRectangle(
            BoardGeometryRectangle rectangle,
            BoardGeometryKind kind,
            int x,
            int y,
            int z,
            int width,
            int depth)
        {
            Assert.That(rectangle.Kind, Is.EqualTo(kind));
            Assert.That(rectangle.X, Is.EqualTo(x));
            Assert.That(rectangle.Y, Is.EqualTo(y));
            Assert.That(rectangle.Z, Is.EqualTo(z));
            Assert.That(rectangle.Width, Is.EqualTo(width));
            Assert.That(rectangle.Depth, Is.EqualTo(depth));
        }
    }
}
