using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace TowerDefense3D.GridPlacement.Tests.EditMode
{
    public sealed class BoardCameraFramingTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = created.Count - 1; index >= 0; index--)
            {
                if (created[index] != null)
                {
                    Object.DestroyImmediate(created[index]);
                }
            }

            created.Clear();
        }

        [Test]
        public void Bounds_UsesLowestSupportAndSameLevelBlockers()
        {
            BoardDefinition board = CreateBoard(
                new GridDimensions(10, 10, 4),
                1f,
                1f,
                new[]
                {
                    Cell(5, 5, 2, BoardCellFlags.SupportsPlacement),
                    Cell(2, 3, 1, BoardCellFlags.SupportsPlacement),
                    Cell(7, 8, 1, BoardCellFlags.StaticBlocker),
                    Cell(0, 0, 0, BoardCellFlags.StaticBlocker),
                    Cell(20, 2, 1, BoardCellFlags.SupportsPlacement),
                });

            bool calculated = LowestBoardLevelBoundsCalculator.TryCalculate(
                board,
                out LowestBoardLevelBounds bounds);

            Assert.That(calculated, Is.True);
            Assert.That(
                bounds,
                Is.EqualTo(new LowestBoardLevelBounds(1, 2, 3, 8, 9)));
        }

        [Test]
        public void Bounds_WithoutSupportDoesNotProduceAFrame()
        {
            BoardDefinition board = CreateBoard(
                new GridDimensions(3, 3, 2),
                1f,
                1f,
                new[]
                {
                    Cell(1, 1, 0, BoardCellFlags.StaticBlocker),
                });

            Assert.That(
                LowestBoardLevelBoundsCalculator.TryCalculate(board, out _),
                Is.False);
        }

        [Test]
        public void Bounds_CameraCapDoesNotChangeFullSparseFootprint()
        {
            BoardDefinition board = CreateBoard(
                new GridDimensions(80, 12, 1),
                1f,
                1f,
                new[]
                {
                    Cell(0, 2, 0, BoardCellFlags.SupportsPlacement),
                    Cell(79, 9, 0, BoardCellFlags.SupportsPlacement),
                },
                maxCameraGridXSpan: 40);

            bool calculated = LowestBoardLevelBoundsCalculator.TryCalculate(
                board,
                out LowestBoardLevelBounds fullBounds);
            bool capped = BoardCameraFramingBounds.TryCreate(
                fullBounds,
                board.MaxCameraGridXSpan,
                board.MaxCameraGridYSpan,
                out BoardCameraFramingBounds framingBounds);

            Assert.That(calculated, Is.True);
            Assert.That(
                fullBounds,
                Is.EqualTo(new LowestBoardLevelBounds(0, 0, 2, 80, 10)));
            Assert.That(capped, Is.True);
            Assert.That(framingBounds.MinX, Is.EqualTo(20f));
            Assert.That(framingBounds.MaxXExclusive, Is.EqualTo(60f));
            Assert.That(framingBounds.CenterX, Is.EqualTo(40f));
            Assert.That(framingBounds.MinZ, Is.EqualTo(2f));
            Assert.That(framingBounds.MaxZExclusive, Is.EqualTo(10f));
        }

        [TestCase(0, 80, 2, 12, 40, 0, 20f, 60f, 2f, 12f, 40f, 7f)]
        [TestCase(2, 9, 3, 11, 4, 5, 3.5f, 7.5f, 4.5f, 9.5f, 5.5f, 7f)]
        [TestCase(-7, 4, -5, 6, 5, 3, -4f, 1f, -1f, 2f, -1.5f, 0.5f)]
        [TestCase(4, 10, -4, 4, 6, 8, 4f, 10f, -4f, 4f, 7f, 0f)]
        [TestCase(-3, 2, 9, 12, 50, 20, -3f, 2f, 9f, 12f, -0.5f, 10.5f)]
        [TestCase(-5, 4, -8, 3, 0, 0, -5f, 4f, -8f, 3f, -0.5f, -2.5f)]
        public void FramingBounds_ApplyIndependentCenteredCaps(
            int minX,
            int maxXExclusive,
            int minZ,
            int maxZExclusive,
            int maxGridXSpan,
            int maxGridYSpan,
            float expectedMinX,
            float expectedMaxXExclusive,
            float expectedMinZ,
            float expectedMaxZExclusive,
            float expectedCenterX,
            float expectedCenterZ)
        {
            var fullBounds = new LowestBoardLevelBounds(
                3,
                minX,
                minZ,
                maxXExclusive,
                maxZExclusive);

            bool capped = BoardCameraFramingBounds.TryCreate(
                fullBounds,
                maxGridXSpan,
                maxGridYSpan,
                out BoardCameraFramingBounds framingBounds);

            Assert.That(capped, Is.True);
            Assert.That(framingBounds.Level, Is.EqualTo(3));
            Assert.That(framingBounds.MinX, Is.EqualTo(expectedMinX).Within(0.0001f));
            Assert.That(framingBounds.MaxXExclusive, Is.EqualTo(expectedMaxXExclusive).Within(0.0001f));
            Assert.That(framingBounds.MinZ, Is.EqualTo(expectedMinZ).Within(0.0001f));
            Assert.That(framingBounds.MaxZExclusive, Is.EqualTo(expectedMaxZExclusive).Within(0.0001f));
            Assert.That(framingBounds.CenterX, Is.EqualTo(expectedCenterX).Within(0.0001f));
            Assert.That(framingBounds.CenterZ, Is.EqualTo(expectedCenterZ).Within(0.0001f));
        }

        [Test]
        public void FramingBounds_NegativeDirectLimitsRemainUnlimited()
        {
            var fullBounds = new LowestBoardLevelBounds(2, -6, 4, 9, 17);

            bool created = BoardCameraFramingBounds.TryCreate(
                fullBounds,
                -3,
                -8,
                out BoardCameraFramingBounds framingBounds);

            Assert.That(created, Is.True);
            Assert.That(framingBounds.MinX, Is.EqualTo(-6f));
            Assert.That(framingBounds.MaxXExclusive, Is.EqualTo(9f));
            Assert.That(framingBounds.MinZ, Is.EqualTo(4f));
            Assert.That(framingBounds.MaxZExclusive, Is.EqualTo(17f));
        }

        [Test]
        public void Plane_UsesCellEdgesPaddingAndBoardTransform()
        {
            BoardDefinition board = CreateBoard(
                new GridDimensions(4, 5, 3),
                2f,
                1.5f,
                new[]
                {
                    Cell(1, 2, 1, BoardCellFlags.SupportsPlacement),
                    Cell(2, 3, 1, BoardCellFlags.StaticBlocker),
                });
            GameObject originObject = Track(new GameObject("Board Origin"));
            Transform origin = originObject.transform;
            origin.SetPositionAndRotation(
                new Vector3(10f, 5f, -3f),
                Quaternion.Euler(0f, 30f, 0f));
            origin.localScale = new Vector3(1.2f, 1f, 0.8f);

            bool createdPlane = BoardCameraFramingPlane.TryCreate(
                board,
                origin,
                1f,
                out BoardCameraFramingPlane plane);

            Assert.That(createdPlane, Is.True);
            Assert.That(
                Vector3.Distance(
                    plane.Corner0,
                    origin.TransformPoint(new Vector3(0f, 1.5f, 2f))),
                Is.LessThan(0.0001f));
            Assert.That(
                Vector3.Distance(
                    plane.Corner2,
                    origin.TransformPoint(new Vector3(8f, 1.5f, 10f))),
                Is.LessThan(0.0001f));
        }

        [Test]
        public void Plane_AppliesIndependentCapsBeforePaddingInBoardSpace()
        {
            BoardDefinition board = CreateBoard(
                new GridDimensions(80, 20, 2),
                2f,
                1.5f,
                new[]
                {
                    Cell(0, 1, 1, BoardCellFlags.SupportsPlacement),
                    Cell(79, 19, 1, BoardCellFlags.SupportsPlacement),
                },
                maxCameraGridXSpan: 40,
                maxCameraGridYSpan: 6);
            GameObject originObject = Track(new GameObject("Capped Board Origin"));
            Transform origin = originObject.transform;
            origin.SetPositionAndRotation(
                new Vector3(-4f, 3f, 11f),
                Quaternion.Euler(0f, -37f, 0f));
            origin.localScale = new Vector3(0.75f, 1f, 1.25f);

            bool createdPlane = BoardCameraFramingPlane.TryCreate(
                board,
                origin,
                1.5f,
                out BoardCameraFramingPlane plane);

            Assert.That(createdPlane, Is.True);
            Assert.That(
                Vector3.Distance(
                    plane.Corner0,
                    origin.TransformPoint(new Vector3(37f, 1.5f, 12f))),
                Is.LessThan(0.0001f));
            Assert.That(
                Vector3.Distance(
                    plane.Corner2,
                    origin.TransformPoint(new Vector3(123f, 1.5f, 30f))),
                Is.LessThan(0.0001f));
            Assert.That(
                Vector3.Distance(
                    plane.Center,
                    origin.TransformPoint(new Vector3(80f, 1.5f, 21f))),
                Is.LessThan(0.0001f));
        }

        [Test]
        public void PlaneAndSolver_RecenterWhenOnlyMaximumEdgesGrow()
        {
            BoardDefinition smallerOverflowBoard = CreateBoard(
                new GridDimensions(80, 12, 1),
                1.25f,
                1f,
                new[]
                {
                    Cell(0, 2, 0, BoardCellFlags.SupportsPlacement),
                    Cell(79, 11, 0, BoardCellFlags.SupportsPlacement),
                },
                maxCameraGridXSpan: 40,
                maxCameraGridYSpan: 6);
            BoardDefinition largerOverflowBoard = CreateBoard(
                new GridDimensions(120, 20, 1),
                1.25f,
                1f,
                new[]
                {
                    Cell(0, 2, 0, BoardCellFlags.SupportsPlacement),
                    Cell(119, 19, 0, BoardCellFlags.SupportsPlacement),
                },
                maxCameraGridXSpan: 40,
                maxCameraGridYSpan: 6);
            GameObject originObject = Track(new GameObject("Overflow Invariance Origin"));
            Transform origin = originObject.transform;
            origin.SetPositionAndRotation(
                new Vector3(7f, 2f, -9f),
                Quaternion.Euler(0f, 23f, 0f));

            bool createdSmaller = BoardCameraFramingPlane.TryCreate(
                smallerOverflowBoard,
                origin,
                1.25f,
                out BoardCameraFramingPlane smallerPlane);
            bool createdLarger = BoardCameraFramingPlane.TryCreate(
                largerOverflowBoard,
                origin,
                1.25f,
                out BoardCameraFramingPlane largerPlane);

            Assert.That(createdSmaller, Is.True);
            Assert.That(createdLarger, Is.True);
            Vector3 expectedCenterDelta = origin.TransformVector(
                new Vector3(25f, 0f, 5f));
            Assert.That(
                Vector3.Distance(
                    largerPlane.Center - smallerPlane.Center,
                    expectedCenterDelta),
                Is.LessThan(0.0001f));
            for (int index = 0; index < 4; index++)
            {
                Assert.That(
                    Vector3.Distance(
                        largerPlane.GetCorner(index) - smallerPlane.GetCorner(index),
                        expectedCenterDelta),
                    Is.LessThan(0.0001f));
            }

            Quaternion cameraRotation = Quaternion.Euler(59.15f, 0.1f, 0f);
            var safeViewport = new Rect(0.05f, 0.08f, 0.9f, 0.84f);
            bool solvedSmaller = BoardCameraFramingSolver.TryCalculatePosition(
                smallerPlane,
                cameraRotation,
                43f,
                20f / 9f,
                0.1f,
                safeViewport,
                out Vector3 smallerPosition);
            bool solvedLarger = BoardCameraFramingSolver.TryCalculatePosition(
                largerPlane,
                cameraRotation,
                43f,
                20f / 9f,
                0.1f,
                safeViewport,
                out Vector3 largerPosition);

            Assert.That(solvedSmaller, Is.True);
            Assert.That(solvedLarger, Is.True);
            Assert.That(
                Vector3.Distance(
                    largerPosition - smallerPosition,
                    expectedCenterDelta),
                Is.LessThan(0.0001f));
        }

        [TestCase(16f / 9f)]
        [TestCase(20f / 9f)]
        [TestCase(4f / 3f)]
        public void Solver_FitsEveryCornerInsideSafeViewport(float aspect)
        {
            var plane = new BoardCameraFramingPlane(
                new Vector3(-7f, 0f, -5f),
                new Vector3(13f, 0f, -5f),
                new Vector3(13f, 0f, 19f),
                new Vector3(-7f, 0f, 19f));
            Quaternion rotation = Quaternion.Euler(59.15f, 0.1f, 0f);
            var safeViewport = new Rect(0.05f, 0.08f, 0.9f, 0.84f);

            bool solved = BoardCameraFramingSolver.TryCalculatePosition(
                plane,
                rotation,
                43f,
                aspect,
                0.1f,
                safeViewport,
                out Vector3 position);

            Assert.That(solved, Is.True);
            for (int index = 0; index < 4; index++)
            {
                Vector3 viewport = ProjectToViewport(
                    plane.GetCorner(index),
                    position,
                    rotation,
                    43f,
                    aspect);
                Assert.That(viewport.z, Is.GreaterThanOrEqualTo(0.1f - 0.0001f));
                Assert.That(viewport.x, Is.InRange(
                    safeViewport.xMin - 0.0001f,
                    safeViewport.xMax + 0.0001f));
                Assert.That(viewport.y, Is.InRange(
                    safeViewport.yMin - 0.0001f,
                    safeViewport.yMax + 0.0001f));
            }
        }

        [Test]
        public void Solver_RejectsInvalidProjectionInputs()
        {
            var plane = new BoardCameraFramingPlane(
                Vector3.zero,
                Vector3.right,
                Vector3.right + Vector3.forward,
                Vector3.forward);

            Assert.That(
                BoardCameraFramingSolver.TryCalculatePosition(
                    plane,
                    Quaternion.identity,
                    0f,
                    16f / 9f,
                    0.1f,
                    new Rect(0f, 0f, 1f, 1f),
                    out _),
                Is.False);
            Assert.That(
                BoardCameraFramingSolver.TryCalculatePosition(
                    plane,
                    Quaternion.identity,
                    60f,
                    16f / 9f,
                    0.1f,
                    new Rect(0.5f, 0.5f, 0f, 0f),
                    out _),
                Is.False);
        }

        [Test]
        public void SafeViewport_CombinesCameraSafeAreaAndInnerComposition()
        {
            bool success = BoardCameraFramer.TryBuildSafeViewportRect(
                new Rect(100f, 50f, 800f, 400f),
                new Rect(140f, 70f, 700f, 360f),
                new Rect(0.1f, 0.2f, 0.8f, 0.6f),
                out Rect result);

            Assert.That(success, Is.True);
            Assert.That(result.x, Is.EqualTo(0.1375f).Within(0.0001f));
            Assert.That(result.y, Is.EqualTo(0.23f).Within(0.0001f));
            Assert.That(result.width, Is.EqualTo(0.7f).Within(0.0001f));
            Assert.That(result.height, Is.EqualTo(0.54f).Within(0.0001f));
        }

        [Test]
        public void Plane_FocusRegionNarrowsToUnionOfFocusFlaggedCellsWithNoCapOrPadding()
        {
            BoardDefinition board = CreateBoard(
                new GridDimensions(10, 10, 1),
                1f,
                1f,
                new[]
                {
                    Cell(0, 0, 0, BoardCellFlags.SupportsPlacement),
                    Cell(9, 9, 0, BoardCellFlags.SupportsPlacement),
                    Cell(2, 2, 0, BoardCellFlags.CameraFocus),
                    Cell(4, 3, 0, BoardCellFlags.CameraFocus),
                });
            Transform origin = Track(new GameObject("Focus Only Origin")).transform;

            bool createdPlane = BoardCameraFramingPlane.TryCreate(
                board,
                origin,
                0f,
                out BoardCameraFramingPlane plane);

            Assert.That(createdPlane, Is.True);
            Assert.That(
                Vector3.Distance(plane.Corner0, new Vector3(2f, 0f, 2f)),
                Is.LessThan(0.0001f));
            Assert.That(
                Vector3.Distance(plane.Corner2, new Vector3(5f, 0f, 4f)),
                Is.LessThan(0.0001f));
        }

        [Test]
        public void Plane_IgnoresCameraFocusCellsOutsideLowestLevelAndFallsBackToFullFootprint()
        {
            BoardDefinition board = CreateBoard(
                new GridDimensions(10, 10, 2),
                1f,
                1f,
                new[]
                {
                    Cell(0, 0, 0, BoardCellFlags.SupportsPlacement),
                    Cell(9, 9, 0, BoardCellFlags.SupportsPlacement),
                    Cell(4, 4, 1, BoardCellFlags.CameraFocus),
                });
            Transform origin = Track(new GameObject("Ignored Focus Origin")).transform;

            bool createdPlane = BoardCameraFramingPlane.TryCreate(
                board,
                origin,
                0f,
                out BoardCameraFramingPlane plane);

            Assert.That(createdPlane, Is.True);
            Assert.That(
                Vector3.Distance(plane.Corner0, new Vector3(0f, 0f, 0f)),
                Is.LessThan(0.0001f));
            Assert.That(
                Vector3.Distance(plane.Corner2, new Vector3(10f, 0f, 10f)),
                Is.LessThan(0.0001f));
        }

        [Test]
        public void Plane_NoCameraFocusCellsProducesSameFramingAsPreFeatureFullFootprintWithCapAndPadding()
        {
            // No CameraFocus bit is set anywhere on this board, so
            // BoardCameraFocusRegionCalculator.TryCalculate must return false and the
            // solver must fall back to exactly the pre-feature full-footprint plus
            // Grid X/Y cap plus edge-padding formula (backward compatibility).
            BoardDefinition board = CreateBoard(
                new GridDimensions(40, 10, 1),
                1f,
                1f,
                new[]
                {
                    Cell(0, 3, 0, BoardCellFlags.SupportsPlacement),
                    Cell(39, 6, 0, BoardCellFlags.SupportsPlacement),
                },
                maxCameraGridXSpan: 10);
            Transform origin = Track(new GameObject("Backward Compat Origin")).transform;

            bool createdPlane = BoardCameraFramingPlane.TryCreate(
                board,
                origin,
                1f,
                out BoardCameraFramingPlane plane);

            Assert.That(createdPlane, Is.True);
            Assert.That(
                Vector3.Distance(plane.Corner0, new Vector3(14f, 0f, 2f)),
                Is.LessThan(0.0001f));
            Assert.That(
                Vector3.Distance(plane.Corner2, new Vector3(26f, 0f, 8f)),
                Is.LessThan(0.0001f));
        }

        [Test]
        public void Plane_ComposesFocusRegionBeforeGridCapBeforeEdgePadding()
        {
            BoardDefinition board = CreateBoard(
                new GridDimensions(20, 20, 2),
                1f,
                1f,
                new[]
                {
                    Cell(0, 0, 0, BoardCellFlags.SupportsPlacement),
                    Cell(19, 19, 0, BoardCellFlags.SupportsPlacement),
                    Cell(5, 5, 0, BoardCellFlags.CameraFocus),
                    Cell(10, 8, 0, BoardCellFlags.CameraFocus),
                },
                maxCameraGridXSpan: 4,
                maxCameraGridYSpan: 2);
            Transform origin = Track(new GameObject("Composition Order Origin")).transform;

            bool createdPlane = BoardCameraFramingPlane.TryCreate(
                board,
                origin,
                1f,
                out BoardCameraFramingPlane plane);

            Assert.That(createdPlane, Is.True);
            // Focus union of (5,5)-(10,8) is X:[5,11) Z:[5,9); the X/Y span cap
            // (4/2) then narrows that focus-centered region to X:[6,10) Z:[6,8);
            // edge padding of 1 cell is applied last, giving X:[5,11] Z:[5,9].
            // Applying the cap to the full 20x20 footprint instead (wrong order)
            // would center on X:10/Z:10 and yield X:[7,13]/Z:[8,12], which does
            // not match these expected corners.
            Assert.That(
                Vector3.Distance(plane.Corner0, new Vector3(5f, 0f, 5f)),
                Is.LessThan(0.0001f));
            Assert.That(
                Vector3.Distance(plane.Corner1, new Vector3(11f, 0f, 5f)),
                Is.LessThan(0.0001f));
            Assert.That(
                Vector3.Distance(plane.Corner2, new Vector3(11f, 0f, 9f)),
                Is.LessThan(0.0001f));
            Assert.That(
                Vector3.Distance(plane.Corner3, new Vector3(5f, 0f, 9f)),
                Is.LessThan(0.0001f));
        }

        private BoardDefinition CreateBoard(
            GridDimensions dimensions,
            float cellSize,
            float heightUnit,
            BoardCellDefinition[] cells,
            int maxCameraGridXSpan = 0,
            int maxCameraGridYSpan = 0)
        {
            BoardDefinition board = Track(ScriptableObject.CreateInstance<BoardDefinition>());
            SetField(board, "dimensions", dimensions);
            SetField(board, "cellSize", cellSize);
            SetField(board, "heightUnit", heightUnit);
            SetField(board, "maxCameraGridXSpan", maxCameraGridXSpan);
            SetField(board, "maxCameraGridYSpan", maxCameraGridYSpan);
            SetField(board, "cells", cells);
            return board;
        }

        private T Track<T>(T value) where T : Object
        {
            created.Add(value);
            return value;
        }

        private static BoardCellDefinition Cell(
            int x,
            int z,
            int y,
            BoardCellFlags flags) =>
            new BoardCellDefinition(new GridCell(x, z, y), flags);

        private static void SetField<T>(Object target, string name, T value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Expected serialized field '{name}'.");
            field.SetValue(target, value);
        }

        private static Vector3 ProjectToViewport(
            Vector3 point,
            Vector3 cameraPosition,
            Quaternion cameraRotation,
            float verticalFieldOfView,
            float aspect)
        {
            Vector3 cameraSpace = Quaternion.Inverse(cameraRotation) * (point - cameraPosition);
            float verticalSlope = Mathf.Tan(verticalFieldOfView * 0.5f * Mathf.Deg2Rad);
            float horizontalSlope = verticalSlope * aspect;
            return new Vector3(
                0.5f + cameraSpace.x / (2f * cameraSpace.z * horizontalSlope),
                0.5f + cameraSpace.y / (2f * cameraSpace.z * verticalSlope),
                cameraSpace.z);
        }
    }
}
