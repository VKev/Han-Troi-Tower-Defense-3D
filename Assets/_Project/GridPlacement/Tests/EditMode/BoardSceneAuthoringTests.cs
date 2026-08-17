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

        [Test]
        public void Synchronizer_ReframesOnlyCameraAssignedToMatchingPresenter()
        {
            BoardDefinition board = CreateBoard(
                new GridDimensions(4, 3, 1),
                true,
                new[]
                {
                    new BoardCellDefinition(
                        new GridCell(0, 0, 0),
                        BoardCellFlags.SupportsPlacement),
                    new BoardCellDefinition(
                        new GridCell(3, 2, 0),
                        BoardCellFlags.StaticBlocker),
                });
            GameObject presenterObject = Track(new GameObject("Board Presenter"));
            presenterObject.transform.SetPositionAndRotation(
                new Vector3(3f, 2f, -4f),
                Quaternion.Euler(0f, 20f, 0f));
            BoardScenePresenter presenter =
                presenterObject.AddComponent<BoardScenePresenter>();
            SetField(presenter, "board", board);

            GameObject cameraObject = Track(new GameObject("Matching Camera"));
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 43f;
            camera.aspect = 16f / 9f;
            camera.nearClipPlane = 0.1f;
            camera.transform.rotation = Quaternion.Euler(60f, 0f, 0f);
            Vector3 originalPosition = new Vector3(50f, 50f, 50f);
            camera.transform.position = originalPosition;
            BoardCameraFramer framer = cameraObject.AddComponent<BoardCameraFramer>();
            SetField(framer, "targetCamera", camera);
            SetField(framer, "boardPresenter", presenter);

            BoardDefinition otherBoard = CreateBoard(
                new GridDimensions(1, 1, 1),
                true,
                new[]
                {
                    new BoardCellDefinition(
                        new GridCell(0, 0, 0),
                        BoardCellFlags.SupportsPlacement),
                });
            GameObject otherPresenterObject = Track(new GameObject("Other Presenter"));
            BoardScenePresenter otherPresenter =
                otherPresenterObject.AddComponent<BoardScenePresenter>();
            SetField(otherPresenter, "board", otherBoard);
            GameObject otherCameraObject = Track(new GameObject("Other Camera"));
            Camera otherCamera = otherCameraObject.AddComponent<Camera>();
            otherCamera.transform.position = originalPosition;
            BoardCameraFramer otherFramer =
                otherCameraObject.AddComponent<BoardCameraFramer>();
            SetField(otherFramer, "targetCamera", otherCamera);
            SetField(otherFramer, "boardPresenter", otherPresenter);

            BoardSceneSynchronizer.Synchronize(board);

            Assert.That(camera.transform.position, Is.Not.EqualTo(originalPosition));
            Assert.That(otherCamera.transform.position, Is.EqualTo(originalPosition));
            Assert.That(
                BoardCameraFramingPlane.TryCreate(
                    board,
                    presenter.transform,
                    1f,
                    out BoardCameraFramingPlane plane),
                Is.True);
            var expectedRect = new Rect(0.05f, 0.08f, 0.9f, 0.84f);
            for (int index = 0; index < 4; index++)
            {
                Vector3 viewportPoint = camera.WorldToViewportPoint(
                    plane.GetCorner(index));
                Assert.That(viewportPoint.z, Is.GreaterThanOrEqualTo(camera.nearClipPlane));
                Assert.That(viewportPoint.x, Is.InRange(
                    expectedRect.xMin - 0.0001f,
                    expectedRect.xMax + 0.0001f));
                Assert.That(viewportPoint.y, Is.InRange(
                    expectedRect.yMin - 0.0001f,
                    expectedRect.yMax + 0.0001f));
            }
        }

        [Test]
        public void Synchronizer_PreservesOverflowGeometryWhileCameraUsesCappedWindow()
        {
            BoardDefinition board = CreateBoard(
                new GridDimensions(80, 40, 1),
                true,
                new[]
                {
                    new BoardCellDefinition(
                        new GridCell(0, 0, 0),
                        BoardCellFlags.SupportsPlacement),
                    new BoardCellDefinition(
                        new GridCell(39, 19, 0),
                        BoardCellFlags.SupportsPlacement),
                });
            SetField(board, "maxCameraGridXSpan", 40);
            SetField(board, "maxCameraGridYSpan", 20);

            GameObject presenterObject = Track(new GameObject("Board Presenter"));
            BoardScenePresenter presenter =
                presenterObject.AddComponent<BoardScenePresenter>();
            SetField(presenter, "board", board);

            GameObject cameraObject = Track(new GameObject("Board Camera"));
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 43f;
            camera.aspect = 16f / 9f;
            camera.nearClipPlane = 0.1f;
            camera.transform.SetPositionAndRotation(
                new Vector3(100f, 100f, 100f),
                Quaternion.Euler(60f, 0f, 0f));
            BoardCameraFramer framer = cameraObject.AddComponent<BoardCameraFramer>();
            SetField(framer, "targetCamera", camera);
            SetField(framer, "boardPresenter", presenter);

            BoardSceneSynchronizer.Synchronize(board);
            Vector3 initialPosition = camera.transform.position;

            SetField(
                board,
                "cells",
                new[]
                {
                    new BoardCellDefinition(
                        new GridCell(0, 0, 0),
                        BoardCellFlags.SupportsPlacement),
                    new BoardCellDefinition(
                        new GridCell(79, 39, 0),
                        BoardCellFlags.SupportsPlacement),
                });
            Assert.That(
                BoardCameraFramingPlane.TryCreate(
                    board,
                    presenter.transform,
                    1f,
                    out BoardCameraFramingPlane plane),
                Is.True);
            Assert.That(plane.Center.x, Is.EqualTo(40f).Within(0.0001f));
            Assert.That(plane.Center.z, Is.EqualTo(20f).Within(0.0001f));
            Assert.That(framer.TryCalculatePosition(out Vector3 expectedPosition), Is.True);

            BoardSceneSynchronizer.Synchronize(board);

            Transform root = presenterObject.transform.Find(
                BoardSceneSynchronizer.GeneratedRootName);
            Assert.That(root, Is.Not.Null);
            Physics.SyncTransforms();
            BoxCollider[] colliders = root.GetComponentsInChildren<BoxCollider>(true);
            bool foundOverflowCell = false;
            for (int index = 0; index < colliders.Length; index++)
            {
                Bounds bounds = colliders[index].bounds;
                if (bounds.max.x >= 79.999f && bounds.max.z >= 39.999f)
                {
                    foundOverflowCell = true;
                    break;
                }
            }

            Assert.That(foundOverflowCell, Is.True);
            Assert.That(
                Vector3.Distance(camera.transform.position, initialPosition),
                Is.GreaterThan(0.0001f));
            Assert.That(
                Vector3.Distance(camera.transform.position, expectedPosition),
                Is.LessThan(0.0001f));
        }

        [Test]
        public void Synchronizer_GeneratesColliderlessCameraFocusOverlayWhenFocusCellsExist()
        {
            BoardDefinition board = CreateBoard(
                new GridDimensions(6, 6, 1),
                true,
                new[]
                {
                    new BoardCellDefinition(
                        new GridCell(0, 0, 0),
                        BoardCellFlags.SupportsPlacement),
                    new BoardCellDefinition(
                        new GridCell(5, 5, 0),
                        BoardCellFlags.SupportsPlacement),
                    new BoardCellDefinition(
                        new GridCell(2, 2, 0),
                        BoardCellFlags.CameraFocus),
                    new BoardCellDefinition(
                        new GridCell(3, 3, 0),
                        BoardCellFlags.CameraFocus),
                });
            GameObject presenterObject = Track(new GameObject("Board Presenter"));
            BoardScenePresenter presenter = presenterObject.AddComponent<BoardScenePresenter>();
            SetField(presenter, "board", board);

            BoardSceneSynchronizer.Synchronize(board);

            Transform root = presenterObject.transform.Find(BoardSceneSynchronizer.GeneratedRootName);
            Assert.That(root, Is.Not.Null);
            Transform focusOverlay = root.Find(BoardSceneSynchronizer.CameraFocusRegionName);
            Assert.That(focusOverlay, Is.Not.Null);

            MeshRenderer renderer = focusOverlay.GetComponent<MeshRenderer>();
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.enabled, Is.True, "Overlay must follow board.VisualizeInScene.");
            Assert.That(renderer.sharedMaterial, Is.Not.Null);
            Assert.That(
                focusOverlay.GetComponents<Collider>(),
                Is.Empty,
                "The Camera Focus Region overlay is pure visual and must carry no collider.");

            // Focus union of (2,2)-(3,3) is X:[2,4) Z:[2,4); center (3,3), span 2x2.
            Assert.That(focusOverlay.localPosition.x, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(focusOverlay.localPosition.z, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(focusOverlay.localScale.x, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(focusOverlay.localScale.y, Is.EqualTo(2f).Within(0.0001f));
            AssertGeneratedNamesAreReadable(root);
        }

        [Test]
        public void Synchronizer_CameraFocusOverlayFollowsVisualizeInSceneToggle()
        {
            BoardDefinition board = CreateBoard(
                new GridDimensions(6, 6, 1),
                false,
                new[]
                {
                    new BoardCellDefinition(
                        new GridCell(0, 0, 0),
                        BoardCellFlags.SupportsPlacement),
                    new BoardCellDefinition(
                        new GridCell(2, 2, 0),
                        BoardCellFlags.CameraFocus),
                });
            GameObject presenterObject = Track(new GameObject("Board Presenter"));
            BoardScenePresenter presenter = presenterObject.AddComponent<BoardScenePresenter>();
            SetField(presenter, "board", board);

            BoardSceneSynchronizer.Synchronize(board);

            Transform root = presenterObject.transform.Find(BoardSceneSynchronizer.GeneratedRootName);
            Transform focusOverlay = root.Find(BoardSceneSynchronizer.CameraFocusRegionName);
            Assert.That(focusOverlay, Is.Not.Null, "Overlay must still be generated even while hidden.");
            Assert.That(
                focusOverlay.GetComponent<MeshRenderer>().enabled,
                Is.False,
                "visualizeInScene = false must hide the overlay.");

            // visualizeInScene is part of the geometry signature (matching the
            // pre-existing behavior for the placement/blocker geometry), so
            // toggling it triggers a full regenerate rather than an in-place
            // ApplyComponentState reuse; re-find the overlay after resync.
            SetField(board, "visualizeInScene", true);
            BoardSceneSynchronizer.Synchronize(board);

            Transform focusOverlayAfterToggle = root.Find(BoardSceneSynchronizer.CameraFocusRegionName);
            Assert.That(focusOverlayAfterToggle, Is.Not.Null);
            Assert.That(
                focusOverlayAfterToggle.GetComponent<MeshRenderer>().enabled,
                Is.True,
                "Ticking Visualize In Scene must show the overlay.");
        }

        [Test]
        public void Synchronizer_OmitsCameraFocusOverlayWhenNoFocusCellsExist()
        {
            BoardDefinition board = CreateBoard(
                new GridDimensions(4, 4, 1),
                true,
                new[]
                {
                    new BoardCellDefinition(
                        new GridCell(0, 0, 0),
                        BoardCellFlags.SupportsPlacement),
                });
            GameObject presenterObject = Track(new GameObject("Board Presenter"));
            BoardScenePresenter presenter = presenterObject.AddComponent<BoardScenePresenter>();
            SetField(presenter, "board", board);

            BoardSceneSynchronizer.Synchronize(board);

            Transform root = presenterObject.transform.Find(BoardSceneSynchronizer.GeneratedRootName);
            Assert.That(root, Is.Not.Null);
            Assert.That(root.Find(BoardSceneSynchronizer.CameraFocusRegionName), Is.Null);
        }

        [Test]
        public void Synchronizer_RemovesCameraFocusOverlayWhenFocusCellsAreCleared()
        {
            BoardDefinition board = CreateBoard(
                new GridDimensions(6, 6, 1),
                true,
                new[]
                {
                    new BoardCellDefinition(
                        new GridCell(0, 0, 0),
                        BoardCellFlags.SupportsPlacement),
                    new BoardCellDefinition(
                        new GridCell(2, 2, 0),
                        BoardCellFlags.CameraFocus),
                });
            GameObject presenterObject = Track(new GameObject("Board Presenter"));
            BoardScenePresenter presenter = presenterObject.AddComponent<BoardScenePresenter>();
            SetField(presenter, "board", board);
            BoardSceneSynchronizer.Synchronize(board);
            Transform root = presenterObject.transform.Find(BoardSceneSynchronizer.GeneratedRootName);
            Assert.That(root.Find(BoardSceneSynchronizer.CameraFocusRegionName), Is.Not.Null);

            SetField(
                board,
                "cells",
                new[]
                {
                    new BoardCellDefinition(
                        new GridCell(0, 0, 0),
                        BoardCellFlags.SupportsPlacement),
                });
            BoardSceneSynchronizer.Synchronize(board);

            Assert.That(root.Find(BoardSceneSynchronizer.CameraFocusRegionName), Is.Null);
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
