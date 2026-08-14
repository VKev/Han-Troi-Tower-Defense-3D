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
        public IEnumerator Framer_SnapsAtStartupAndOnlyReactsToChangedInputs()
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
            BoardScenePresenter presenter =
                presenterObject.AddComponent<BoardScenePresenter>();
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
            BoardCameraFramer framer = cameraObject.AddComponent<BoardCameraFramer>();
            SetField(framer, "targetCamera", camera);
            SetField(framer, "boardPresenter", presenter);

            Vector3 initialPosition = camera.transform.position;
            cameraObject.SetActive(true);
            yield return null;

            Vector3 landscapePosition = camera.transform.position;
            Assert.That(landscapePosition, Is.Not.EqualTo(initialPosition));

            Vector3 manualOffsetPosition = landscapePosition + Vector3.one;
            camera.transform.position = manualOffsetPosition;
            yield return null;
            Assert.That(camera.transform.position, Is.EqualTo(manualOffsetPosition));

            camera.rect = new Rect(0f, 0f, 0.75f, 1f);
            camera.aspect = 4f / 3f;
            yield return null;
            Assert.That(camera.transform.position, Is.Not.EqualTo(manualOffsetPosition));
            Assert.That(camera.transform.position, Is.Not.EqualTo(landscapePosition));

            Object.Destroy(cameraObject);
            Object.Destroy(presenterObject);
            Object.Destroy(board);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Framer_CappedPoseRecentersWhenOnlyMaximumEdgesGrow()
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
            BoardScenePresenter presenter =
                presenterObject.AddComponent<BoardScenePresenter>();
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
            BoardCameraFramer framer = cameraObject.AddComponent<BoardCameraFramer>();
            SetField(framer, "targetCamera", camera);
            SetField(framer, "boardPresenter", presenter);

            cameraObject.SetActive(true);
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
                framer.TryCalculatePosition(out Vector3 expectedPosition),
                Is.True);
            Assert.That(framer.FrameNow(), Is.True);
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
