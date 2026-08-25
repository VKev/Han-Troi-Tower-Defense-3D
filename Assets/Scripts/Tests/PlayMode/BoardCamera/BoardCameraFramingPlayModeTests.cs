using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TowerDefense3D.GridPlacement.Tests.PlayMode
{
    public sealed class BoardCameraFramingPlayModeTests
    {
        [UnityTest]
        public IEnumerator CameraSystem_SnapsAtStartupAndOnlyReactsToChangedInputs()
        {
            BoardDefinition board = ScriptableObject.CreateInstance<BoardDefinition>();
            SetField(board, "dimensions", new GridDimensions(20, 2, 1));
            SetField(board, "cellSize", 1f);
            SetField(board, "heightUnit", 1f);
            SetField(
                board,
                "cells",
                new[]
                {
                    new BoardCellDefinition(
                        new GridCell(0, 0, 0),
                        BoardCellFlags.SupportsPlacement),
                    new BoardCellDefinition(
                        new GridCell(19, 1, 0),
                        BoardCellFlags.SupportsPlacement),
                });

            var presenterObject = new GameObject("Runtime Board Presenter");
            presenterObject.transform.SetPositionAndRotation(
                new Vector3(2f, 0f, -3f),
                Quaternion.Euler(0f, 15f, 0f));
            BoardView presenter =
                presenterObject.AddComponent<BoardView>();
            SetField(presenter, "board", board);

            var cameraObject = new GameObject("Runtime Board Camera");
            cameraObject.SetActive(false);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 43f;
            camera.aspect = 16f / 9f;
            camera.nearClipPlane = 0.1f;
            camera.transform.SetPositionAndRotation(
                new Vector3(40f, 40f, 40f),
                Quaternion.Euler(60f, 0f, 0f));
            BoardCameraView framer = cameraObject.AddComponent<BoardCameraView>();
            SetField(framer, "targetCamera", camera);
            SetField(framer, "boardView", presenter);

            Vector3 initialPosition = camera.transform.position;
            cameraObject.SetActive(true);
            var cameraSystem = new BoardCameraSystem(framer);
            cameraSystem.Start();
            yield return null;

            Vector3 landscapePosition = camera.transform.position;
            Assert.That(landscapePosition, Is.Not.EqualTo(initialPosition));

            Vector3 manualOffsetPosition = landscapePosition + Vector3.one;
            camera.transform.position = manualOffsetPosition;
            cameraSystem.LateTick();
            yield return null;
            Assert.That(camera.transform.position, Is.EqualTo(manualOffsetPosition));

            camera.rect = new Rect(0f, 0f, 0.75f, 1f);
            camera.aspect = 4f / 3f;
            cameraSystem.LateTick();
            yield return null;
            Assert.That(camera.transform.position, Is.Not.EqualTo(manualOffsetPosition));
            Assert.That(camera.transform.position, Is.Not.EqualTo(landscapePosition));

            Object.Destroy(cameraObject);
            Object.Destroy(presenterObject);
            Object.Destroy(board);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CameraSystem_CappedPoseRecentersWhenOnlyMaximumEdgesGrow()
        {
            BoardDefinition board = ScriptableObject.CreateInstance<BoardDefinition>();
            SetField(board, "dimensions", new GridDimensions(80, 40, 1));
            SetField(board, "cellSize", 1f);
            SetField(board, "heightUnit", 1f);
            SetField(board, "maxCameraGridXSpan", 40);
            SetField(board, "maxCameraGridYSpan", 20);
            SetField(
                board,
                "cells",
                new[]
                {
                    new BoardCellDefinition(
                        new GridCell(0, 0, 0),
                        BoardCellFlags.SupportsPlacement),
                    new BoardCellDefinition(
                        new GridCell(39, 19, 0),
                        BoardCellFlags.SupportsPlacement),
                });

            var presenterObject = new GameObject("Runtime Board Presenter");
            BoardView presenter =
                presenterObject.AddComponent<BoardView>();
            SetField(presenter, "board", board);

            var cameraObject = new GameObject("Runtime Board Camera");
            cameraObject.SetActive(false);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 43f;
            camera.aspect = 16f / 9f;
            camera.nearClipPlane = 0.1f;
            camera.transform.SetPositionAndRotation(
                new Vector3(100f, 100f, 100f),
                Quaternion.Euler(60f, 0f, 0f));
            BoardCameraView framer = cameraObject.AddComponent<BoardCameraView>();
            SetField(framer, "targetCamera", camera);
            SetField(framer, "boardView", presenter);

            cameraObject.SetActive(true);
            var cameraSystem = new BoardCameraSystem(framer);
            cameraSystem.Start();
            yield return null;
            Vector3 cappedPosition = camera.transform.position;
            Assert.That(
                BoardCameraFramingPlane.TryCreate(
                    board,
                    presenter.transform,
                    1f,
                    out BoardCameraFramingPlane initialPlane),
                Is.True);
            Assert.That(initialPlane.Center.x, Is.EqualTo(20f).Within(0.0001f));
            Assert.That(initialPlane.Center.z, Is.EqualTo(10f).Within(0.0001f));

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
                LowestBoardLevelBoundsCalculator.TryCalculate(
                    board,
                    out LowestBoardLevelBounds expandedBounds),
                Is.True);
            Assert.That(expandedBounds.MaxXExclusive, Is.EqualTo(80));
            Assert.That(expandedBounds.MaxZExclusive, Is.EqualTo(40));
            Assert.That(
                BoardCameraFramingPlane.TryCreate(
                    board,
                    presenter.transform,
                    1f,
                    out BoardCameraFramingPlane expandedPlane),
                Is.True);
            Assert.That(expandedPlane.Center.x, Is.EqualTo(40f).Within(0.0001f));
            Assert.That(expandedPlane.Center.z, Is.EqualTo(20f).Within(0.0001f));
            Assert.That(
                cameraSystem.TryCalculatePosition(out Vector3 expectedPosition),
                Is.True);
            Assert.That(cameraSystem.FrameNow(), Is.True);
            yield return null;

            Assert.That(
                Vector3.Distance(camera.transform.position, cappedPosition),
                Is.GreaterThan(0.0001f));
            Assert.That(
                Vector3.Distance(camera.transform.position, expectedPosition),
                Is.LessThan(0.0001f));

            Object.Destroy(cameraObject);
            Object.Destroy(presenterObject);
            Object.Destroy(board);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CameraSystem_FocusRegionCellsNarrowFramingBelowFullFootprint()
        {
            const int maxCameraGridXSpan = 0;
            const int maxCameraGridYSpan = 0;
            const float edgePaddingCells = 1f;

            BoardDefinition fullFootprintBoard = ScriptableObject.CreateInstance<BoardDefinition>();
            SetField(fullFootprintBoard, "dimensions", new GridDimensions(40, 20, 1));
            SetField(fullFootprintBoard, "cellSize", 1f);
            SetField(fullFootprintBoard, "heightUnit", 1f);
            SetField(fullFootprintBoard, "maxCameraGridXSpan", maxCameraGridXSpan);
            SetField(fullFootprintBoard, "maxCameraGridYSpan", maxCameraGridYSpan);
            SetField(
                fullFootprintBoard,
                "cells",
                new[]
                {
                    new BoardCellDefinition(
                        new GridCell(0, 0, 0),
                        BoardCellFlags.SupportsPlacement),
                    new BoardCellDefinition(
                        new GridCell(39, 19, 0),
                        BoardCellFlags.SupportsPlacement),
                });

            BoardDefinition focusRegionBoard = ScriptableObject.CreateInstance<BoardDefinition>();
            SetField(focusRegionBoard, "dimensions", new GridDimensions(40, 20, 1));
            SetField(focusRegionBoard, "cellSize", 1f);
            SetField(focusRegionBoard, "heightUnit", 1f);
            SetField(focusRegionBoard, "maxCameraGridXSpan", maxCameraGridXSpan);
            SetField(focusRegionBoard, "maxCameraGridYSpan", maxCameraGridYSpan);
            SetField(
                focusRegionBoard,
                "cells",
                new[]
                {
                    new BoardCellDefinition(
                        new GridCell(0, 0, 0),
                        BoardCellFlags.SupportsPlacement),
                    new BoardCellDefinition(
                        new GridCell(39, 19, 0),
                        BoardCellFlags.SupportsPlacement),
                    new BoardCellDefinition(
                        new GridCell(10, 5, 0),
                        BoardCellFlags.CameraFocus),
                    new BoardCellDefinition(
                        new GridCell(19, 9, 0),
                        BoardCellFlags.CameraFocus),
                });

            // Both boards share the same lowest-level full footprint and the
            // same grid cap / edge padding inputs; only the presence of
            // CameraFocus cells differs, isolating the focus-narrowing effect.
            Assert.That(
                LowestBoardLevelBoundsCalculator.TryCalculate(
                    fullFootprintBoard,
                    out LowestBoardLevelBounds fullFootprintBounds),
                Is.True);
            Assert.That(
                LowestBoardLevelBoundsCalculator.TryCalculate(
                    focusRegionBoard,
                    out LowestBoardLevelBounds focusBoardFullBounds),
                Is.True);
            Assert.That(focusBoardFullBounds, Is.EqualTo(fullFootprintBounds));

            Assert.That(
                BoardCameraFocusRegionCalculator.TryCalculate(
                    fullFootprintBoard,
                    fullFootprintBounds,
                    out _),
                Is.False,
                "Board with no CameraFocus cells must not narrow the framing region.");
            Assert.That(
                BoardCameraFocusRegionCalculator.TryCalculate(
                    focusRegionBoard,
                    focusBoardFullBounds,
                    out LowestBoardLevelBounds focusBounds),
                Is.True);
            Assert.That(focusBounds.MinX, Is.EqualTo(10));
            Assert.That(focusBounds.MinZ, Is.EqualTo(5));
            Assert.That(focusBounds.MaxXExclusive, Is.EqualTo(20));
            Assert.That(focusBounds.MaxZExclusive, Is.EqualTo(10));

            var fullFootprintPresenterObject = new GameObject("Full Footprint Board Presenter");
            BoardView fullFootprintPresenter =
                fullFootprintPresenterObject.AddComponent<BoardView>();
            SetField(fullFootprintPresenter, "board", fullFootprintBoard);

            var focusPresenterObject = new GameObject("Focus Region Board Presenter");
            BoardView focusPresenter =
                focusPresenterObject.AddComponent<BoardView>();
            SetField(focusPresenter, "board", focusRegionBoard);

            var fullFootprintCameraObject = new GameObject("Full Footprint Board Camera");
            fullFootprintCameraObject.SetActive(false);
            Camera fullFootprintCamera = fullFootprintCameraObject.AddComponent<Camera>();
            fullFootprintCamera.fieldOfView = 43f;
            fullFootprintCamera.aspect = 16f / 9f;
            fullFootprintCamera.nearClipPlane = 0.1f;
            fullFootprintCamera.transform.SetPositionAndRotation(
                new Vector3(100f, 100f, 100f),
                Quaternion.Euler(60f, 0f, 0f));
            BoardCameraView fullFootprintFramer =
                fullFootprintCameraObject.AddComponent<BoardCameraView>();
            SetField(fullFootprintFramer, "targetCamera", fullFootprintCamera);
            SetField(fullFootprintFramer, "boardView", fullFootprintPresenter);

            var focusCameraObject = new GameObject("Focus Region Board Camera");
            focusCameraObject.SetActive(false);
            Camera focusCamera = focusCameraObject.AddComponent<Camera>();
            focusCamera.fieldOfView = 43f;
            focusCamera.aspect = 16f / 9f;
            focusCamera.nearClipPlane = 0.1f;
            focusCamera.transform.SetPositionAndRotation(
                new Vector3(100f, 100f, 100f),
                Quaternion.Euler(60f, 0f, 0f));
            BoardCameraView focusFramer =
                focusCameraObject.AddComponent<BoardCameraView>();
            SetField(focusFramer, "targetCamera", focusCamera);
            SetField(focusFramer, "boardView", focusPresenter);

            fullFootprintCameraObject.SetActive(true);
            focusCameraObject.SetActive(true);
            var fullFootprintCameraSystem = new BoardCameraSystem(fullFootprintFramer);
            var focusCameraSystem = new BoardCameraSystem(focusFramer);
            fullFootprintCameraSystem.Start();
            focusCameraSystem.Start();
            yield return null;

            Assert.That(
                BoardCameraFramingPlane.TryCreate(
                    fullFootprintBoard,
                    fullFootprintPresenter.transform,
                    edgePaddingCells,
                    out BoardCameraFramingPlane fullFootprintPlane),
                Is.True);
            Assert.That(
                BoardCameraFramingPlane.TryCreate(
                    focusRegionBoard,
                    focusPresenter.transform,
                    edgePaddingCells,
                    out BoardCameraFramingPlane focusPlane),
                Is.True);

            // Different focus vs. full-footprint centers plus a tighter
            // camera distance for the same FOV/rotation prove the framing
            // result is both different and visibly narrower, not merely
            // repositioned.
            Assert.That(focusPlane.Center, Is.Not.EqualTo(fullFootprintPlane.Center));
            Assert.That(fullFootprintPlane.Center.x, Is.EqualTo(20f).Within(0.0001f));
            Assert.That(fullFootprintPlane.Center.z, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(focusPlane.Center.x, Is.EqualTo(15f).Within(0.0001f));
            Assert.That(focusPlane.Center.z, Is.EqualTo(7.5f).Within(0.0001f));

            float fullFootprintDistance = Vector3.Distance(
                fullFootprintCamera.transform.position,
                fullFootprintPlane.Center);
            float focusDistance = Vector3.Distance(
                focusCamera.transform.position,
                focusPlane.Center);
            Assert.That(
                focusDistance,
                Is.LessThan(fullFootprintDistance),
                "A focus-narrowed region should require a tighter (closer) camera "
                + "framing distance than the full footprint for the same FOV and rotation.");
            Assert.That(
                focusCamera.transform.position,
                Is.Not.EqualTo(fullFootprintCamera.transform.position));

            Object.Destroy(fullFootprintCameraObject);
            Object.Destroy(focusCameraObject);
            Object.Destroy(fullFootprintPresenterObject);
            Object.Destroy(focusPresenterObject);
            Object.Destroy(fullFootprintBoard);
            Object.Destroy(focusRegionBoard);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CameraSystem_NoFocusCellsMatchesFullFootprintFraming()
        {
            const float edgePaddingCells = 1f;

            BoardDefinition board = ScriptableObject.CreateInstance<BoardDefinition>();
            SetField(board, "dimensions", new GridDimensions(20, 10, 1));
            SetField(board, "cellSize", 1f);
            SetField(board, "heightUnit", 1f);
            SetField(
                board,
                "cells",
                new[]
                {
                    new BoardCellDefinition(
                        new GridCell(0, 0, 0),
                        BoardCellFlags.SupportsPlacement),
                    new BoardCellDefinition(
                        new GridCell(19, 9, 0),
                        BoardCellFlags.SupportsPlacement),
                });

            var presenterObject = new GameObject("Runtime Board Presenter");
            BoardView presenter =
                presenterObject.AddComponent<BoardView>();
            SetField(presenter, "board", board);

            var cameraObject = new GameObject("Runtime Board Camera");
            cameraObject.SetActive(false);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 43f;
            camera.aspect = 16f / 9f;
            camera.nearClipPlane = 0.1f;
            camera.transform.SetPositionAndRotation(
                new Vector3(50f, 50f, 50f),
                Quaternion.Euler(60f, 0f, 0f));
            BoardCameraView framer = cameraObject.AddComponent<BoardCameraView>();
            SetField(framer, "targetCamera", camera);
            SetField(framer, "boardView", presenter);

            // No cell carries CameraFocus, so the focus-region step must fall
            // back to exactly the pre-feature full-lowest-level-footprint
            // bounds: this is the explicit backward-compatibility proof.
            Assert.That(
                LowestBoardLevelBoundsCalculator.TryCalculate(
                    board,
                    out LowestBoardLevelBounds fullBounds),
                Is.True);
            Assert.That(
                BoardCameraFocusRegionCalculator.TryCalculate(
                    board,
                    fullBounds,
                    out _),
                Is.False);
            Assert.That(
                BoardCameraFramingBounds.TryCreate(
                    fullBounds,
                    board.MaxCameraGridXSpan,
                    board.MaxCameraGridYSpan,
                    out BoardCameraFramingBounds expectedFramingBounds),
                Is.True);

            cameraObject.SetActive(true);
            var cameraSystem = new BoardCameraSystem(framer);
            cameraSystem.Start();
            yield return null;

            Assert.That(
                BoardCameraFramingPlane.TryCreate(
                    board,
                    presenter.transform,
                    edgePaddingCells,
                    out BoardCameraFramingPlane plane),
                Is.True);
            Assert.That(
                plane.Center.x,
                Is.EqualTo(expectedFramingBounds.CenterX).Within(0.0001f));
            Assert.That(
                plane.Center.z,
                Is.EqualTo(expectedFramingBounds.CenterZ).Within(0.0001f));

            Assert.That(
                cameraSystem.TryCalculatePosition(out Vector3 expectedPosition),
                Is.True);
            Assert.That(
                Vector3.Distance(camera.transform.position, expectedPosition),
                Is.LessThan(0.0001f));

            Object.Destroy(cameraObject);
            Object.Destroy(presenterObject);
            Object.Destroy(board);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CameraSystem_BoardOffsetsPersistWhenProjectionChanges()
        {
            BoardDefinition board = ScriptableObject.CreateInstance<BoardDefinition>();
            SetField(board, "dimensions", new GridDimensions(12, 8, 1));
            SetField(board, "cellSize", 1f);
            SetField(board, "heightUnit", 1f);
            SetField(
                board,
                "cells",
                new[]
                {
                    new BoardCellDefinition(
                        new GridCell(0, 0, 0),
                        BoardCellFlags.SupportsPlacement),
                    new BoardCellDefinition(
                        new GridCell(11, 7, 0),
                        BoardCellFlags.SupportsPlacement),
                });

            var presenterObject = new GameObject("Offset Board Presenter");
            BoardView presenter =
                presenterObject.AddComponent<BoardView>();
            SetField(presenter, "board", board);

            var cameraObject = new GameObject("Offset Board Camera");
            cameraObject.SetActive(false);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 43f;
            camera.aspect = 16f / 9f;
            camera.nearClipPlane = 0.1f;
            Quaternion baseRotation = Quaternion.Euler(61f, 5f, 0f);
            camera.transform.rotation = baseRotation;
            BoardCameraView framer =
                cameraObject.AddComponent<BoardCameraView>();
            SetField(framer, "targetCamera", camera);
            SetField(framer, "boardView", presenter);

            var positionOffset = new Vector3(1.5f, 0.25f, -1f);
            var rotationOffset = new Vector3(2f, 6f, 0f);
            SetField(
                board,
                "cameraRotationOffsetEuler",
                rotationOffset);
            SetField(board, "cameraPositionOffset", Vector3.zero);

            var cameraSystem = new BoardCameraSystem(framer);
            Assert.That(
                cameraSystem.TryCalculatePose(
                    out Vector3 fittedPosition,
                    out Quaternion expectedRotation),
                Is.True);
            SetField(
                board,
                "cameraPositionOffset",
                positionOffset);
            Assert.That(
                cameraSystem.TryCalculatePose(
                    out Vector3 offsetPosition,
                    out Quaternion offsetRotation),
                Is.True);
            Assert.That(
                Quaternion.Angle(
                    expectedRotation,
                    baseRotation * Quaternion.Euler(rotationOffset)),
                Is.LessThan(0.0001f));
            Assert.That(
                Quaternion.Angle(offsetRotation, expectedRotation),
                Is.LessThan(0.0001f));
            Assert.That(
                Vector3.Distance(
                    offsetPosition - fittedPosition,
                    expectedRotation * positionOffset),
                Is.LessThan(0.0001f));

            cameraObject.SetActive(true);
            cameraSystem.Start();
            yield return null;
            Assert.That(
                Vector3.Distance(camera.transform.position, offsetPosition),
                Is.LessThan(0.0001f));
            Assert.That(
                Quaternion.Angle(camera.transform.rotation, expectedRotation),
                Is.LessThan(0.0001f));

            camera.rect = new Rect(0f, 0f, 0.75f, 1f);
            camera.aspect = 4f / 3f;
            Assert.That(
                cameraSystem.TryCalculatePose(
                    out Vector3 reframedPosition,
                    out Quaternion reframedRotation),
                Is.True);
            cameraSystem.LateTick();
            yield return null;
            Assert.That(
                Vector3.Distance(camera.transform.position, reframedPosition),
                Is.LessThan(0.0001f));
            Assert.That(
                Quaternion.Angle(camera.transform.rotation, reframedRotation),
                Is.LessThan(0.0001f));
            Assert.That(
                Quaternion.Angle(reframedRotation, expectedRotation),
                Is.LessThan(0.0001f));

            Object.Destroy(cameraObject);
            Object.Destroy(presenterObject);
            Object.Destroy(board);
            yield return null;
        }

        private static void SetField<T>(Object target, string fieldName, T value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Expected serialized field '{fieldName}'.");
            field.SetValue(target, value);
        }
    }
}
