using System;
using TowerDefense3D.Core;
using UnityEngine;

namespace TowerDefense3D.GridPlacement
{
    public readonly struct BoardCameraFramingBounds
    {
        private BoardCameraFramingBounds(
            int level,
            float minX,
            float minZ,
            float maxXExclusive,
            float maxZExclusive)
        {
            Level = level;
            MinX = minX;
            MinZ = minZ;
            MaxXExclusive = maxXExclusive;
            MaxZExclusive = maxZExclusive;
        }

        public int Level { get; }
        public float MinX { get; }
        public float MinZ { get; }
        public float MaxXExclusive { get; }
        public float MaxZExclusive { get; }
        public float SpanX => MaxXExclusive - MinX;
        public float SpanZ => MaxZExclusive - MinZ;
        public float CenterX => MinX + SpanX * 0.5f;
        public float CenterZ => MinZ + SpanZ * 0.5f;

        public static bool TryCreate(
            LowestBoardLevelBounds fullBounds,
            int maxGridXSpan,
            int maxGridYSpan,
            out BoardCameraFramingBounds framingBounds)
        {
            framingBounds = default;
            int fullXSpan = fullBounds.MaxXExclusive - fullBounds.MinX;
            int fullZSpan = fullBounds.MaxZExclusive - fullBounds.MinZ;
            if (fullXSpan <= 0 || fullZSpan <= 0)
            {
                return false;
            }

            int framedXSpan = maxGridXSpan > 0
                ? Math.Min(fullXSpan, maxGridXSpan)
                : fullXSpan;
            int framedZSpan = maxGridYSpan > 0
                ? Math.Min(fullZSpan, maxGridYSpan)
                : fullZSpan;
            float fullCenterX = fullBounds.MinX + fullXSpan * 0.5f;
            float fullCenterZ = fullBounds.MinZ + fullZSpan * 0.5f;
            float halfFramedXSpan = framedXSpan * 0.5f;
            float halfFramedZSpan = framedZSpan * 0.5f;
            framingBounds = new BoardCameraFramingBounds(
                fullBounds.Level,
                fullCenterX - halfFramedXSpan,
                fullCenterZ - halfFramedZSpan,
                fullCenterX + halfFramedXSpan,
                fullCenterZ + halfFramedZSpan);
            return true;
        }
    }

    public readonly struct BoardCameraFramingPlane
    {
        public BoardCameraFramingPlane(
            Vector3 corner0,
            Vector3 corner1,
            Vector3 corner2,
            Vector3 corner3)
        {
            Corner0 = corner0;
            Corner1 = corner1;
            Corner2 = corner2;
            Corner3 = corner3;
        }

        public Vector3 Corner0 { get; }
        public Vector3 Corner1 { get; }
        public Vector3 Corner2 { get; }
        public Vector3 Corner3 { get; }
        public Vector3 Center => (Corner0 + Corner1 + Corner2 + Corner3) * 0.25f;

        public Vector3 GetCorner(int index)
        {
            switch (index)
            {
                case 0:
                    return Corner0;
                case 1:
                    return Corner1;
                case 2:
                    return Corner2;
                case 3:
                    return Corner3;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(index));
            }
        }

        public static bool TryCreate(
            BoardDefinition board,
            Transform boardOrigin,
            float edgePaddingCells,
            out BoardCameraFramingPlane plane)
        {
            return TryCreate(
                board,
                boardOrigin,
                edgePaddingCells,
                board != null ? board.MaxCameraGridXSpan : 0,
                board != null ? board.MaxCameraGridYSpan : 0,
                out plane);
        }

        public static bool TryCreate(
            BoardDefinition board,
            Transform boardOrigin,
            float edgePaddingCells,
            int maxGridXSpan,
            int maxGridYSpan,
            out BoardCameraFramingPlane plane)
        {
            plane = default;
            if (board == null || boardOrigin == null
                || !FiniteNumber.IsFinite(board.CellSize) || board.CellSize <= 0f
                || !FiniteNumber.IsFinite(board.HeightUnit) || board.HeightUnit <= 0f
                || !FiniteNumber.IsFinite(edgePaddingCells) || edgePaddingCells < 0f
                || !LowestBoardLevelBoundsCalculator.TryCalculate(
                    board,
                    out LowestBoardLevelBounds fullBounds))
            {
                return false;
            }

            LowestBoardLevelBounds baseBounds =
                BoardCameraFocusRegionCalculator.TryCalculate(
                    board,
                    fullBounds,
                    out LowestBoardLevelBounds focusBounds)
                    ? focusBounds
                    : fullBounds;

            if (!BoardCameraFramingBounds.TryCreate(
                    baseBounds,
                    maxGridXSpan,
                    maxGridYSpan,
                    out BoardCameraFramingBounds framingBounds))
            {
                return false;
            }

            float minX = (framingBounds.MinX - edgePaddingCells) * board.CellSize;
            float maxX = (framingBounds.MaxXExclusive + edgePaddingCells) * board.CellSize;
            float minZ = (framingBounds.MinZ - edgePaddingCells) * board.CellSize;
            float maxZ = (framingBounds.MaxZExclusive + edgePaddingCells) * board.CellSize;
            float levelY = framingBounds.Level * board.HeightUnit;

            plane = new BoardCameraFramingPlane(
                boardOrigin.TransformPoint(new Vector3(minX, levelY, minZ)),
                boardOrigin.TransformPoint(new Vector3(maxX, levelY, minZ)),
                boardOrigin.TransformPoint(new Vector3(maxX, levelY, maxZ)),
                boardOrigin.TransformPoint(new Vector3(minX, levelY, maxZ)));
            return true;
        }
    }

    public static class BoardCameraFramingSolver
    {
        private const float MinimumDenominator = 0.00001f;

        public static bool TryCalculatePosition(
            BoardCameraFramingPlane plane,
            Quaternion cameraRotation,
            float verticalFieldOfView,
            float aspect,
            float nearClipPlane,
            Rect safeViewportRect,
            out Vector3 cameraPosition)
        {
            cameraPosition = default;
            if (!HasValidInputs(
                    verticalFieldOfView,
                    aspect,
                    nearClipPlane,
                    safeViewportRect))
            {
                return false;
            }

            float verticalSlope = Mathf.Tan(verticalFieldOfView * 0.5f * Mathf.Deg2Rad);
            float horizontalSlope = verticalSlope * aspect;
            float leftSlope = (safeViewportRect.xMin * 2f - 1f) * horizontalSlope;
            float rightSlope = (safeViewportRect.xMax * 2f - 1f) * horizontalSlope;
            float bottomSlope = (safeViewportRect.yMin * 2f - 1f) * verticalSlope;
            float topSlope = (safeViewportRect.yMax * 2f - 1f) * verticalSlope;
            float centerHorizontalSlope = (leftSlope + rightSlope) * 0.5f;
            float centerVerticalSlope = (bottomSlope + topSlope) * 0.5f;

            float leftDenominator = centerHorizontalSlope - leftSlope;
            float rightDenominator = rightSlope - centerHorizontalSlope;
            float bottomDenominator = centerVerticalSlope - bottomSlope;
            float topDenominator = topSlope - centerVerticalSlope;
            if (leftDenominator <= MinimumDenominator
                || rightDenominator <= MinimumDenominator
                || bottomDenominator <= MinimumDenominator
                || topDenominator <= MinimumDenominator)
            {
                return false;
            }

            Vector3 right = cameraRotation * Vector3.right;
            Vector3 up = cameraRotation * Vector3.up;
            Vector3 forward = cameraRotation * Vector3.forward;
            Vector3 center = plane.Center;
            float distance = 0f;

            for (int index = 0; index < 4; index++)
            {
                Vector3 offset = plane.GetCorner(index) - center;
                float horizontal = Vector3.Dot(offset, right);
                float vertical = Vector3.Dot(offset, up);
                float depth = Vector3.Dot(offset, forward);

                distance = Mathf.Max(
                    distance,
                    (leftSlope * depth - horizontal) / leftDenominator);
                distance = Mathf.Max(
                    distance,
                    (horizontal - rightSlope * depth) / rightDenominator);
                distance = Mathf.Max(
                    distance,
                    (bottomSlope * depth - vertical) / bottomDenominator);
                distance = Mathf.Max(
                    distance,
                    (vertical - topSlope * depth) / topDenominator);
                distance = Mathf.Max(distance, nearClipPlane - depth);
            }

            if (!FiniteNumber.IsFinite(distance))
            {
                return false;
            }

            cameraPosition = center
                - forward * distance
                - right * (centerHorizontalSlope * distance)
                - up * (centerVerticalSlope * distance);
            return FiniteNumber.IsFinite(cameraPosition.x)
                && FiniteNumber.IsFinite(cameraPosition.y)
                && FiniteNumber.IsFinite(cameraPosition.z);
        }

        private static bool HasValidInputs(
            float verticalFieldOfView,
            float aspect,
            float nearClipPlane,
            Rect safeViewportRect) =>
            FiniteNumber.IsFinite(verticalFieldOfView)
            && verticalFieldOfView > 0f
            && verticalFieldOfView < 179f
            && FiniteNumber.IsFinite(aspect)
            && aspect > 0f
            && FiniteNumber.IsFinite(nearClipPlane)
            && nearClipPlane > 0f
            && FiniteNumber.IsFinite(safeViewportRect.xMin)
            && FiniteNumber.IsFinite(safeViewportRect.xMax)
            && FiniteNumber.IsFinite(safeViewportRect.yMin)
            && FiniteNumber.IsFinite(safeViewportRect.yMax)
            && safeViewportRect.xMin >= 0f
            && safeViewportRect.yMin >= 0f
            && safeViewportRect.xMax <= 1f
            && safeViewportRect.yMax <= 1f
            && safeViewportRect.width > 0f
            && safeViewportRect.height > 0f;
    }
}
