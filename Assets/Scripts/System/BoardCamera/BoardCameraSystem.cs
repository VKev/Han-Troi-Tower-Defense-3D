using System;
using UnityEngine;

namespace TowerDefense3D.GridPlacement
{
    /// <summary>
    /// Reframes one authored board camera when its framing inputs change.
    /// </summary>
    public sealed class BoardCameraSystem
    {
        private const string InvalidFramingWarning =
            "Board camera framing skipped because its Camera, Board, perspective lens, "
            + "or playable Board footprint is invalid.";

        private readonly IBoardCameraView view;

        private BoardDefinition observedBoard;
        private int observedMaxCameraGridXSpan;
        private int observedMaxCameraGridYSpan;
        private Rect observedPixelRect;
        private Rect observedSafeArea;
        private Rect observedCompositionRect;
        private float observedAspect;
        private float observedFieldOfView;
        private float observedNearClip;
        private float observedPadding;
        private bool observedOrthographic;
        private Vector3 observedCameraLocalPositionOffset;
        private Vector3 observedCameraLocalRotationOffsetEuler;
        private Vector3 observedAuthoredBaseRotationEuler;
        private Vector3 observedBoardPosition;
        private Quaternion observedBoardRotation;
        private Vector3 observedBoardScale;
        private Quaternion observedCameraRotation;
        private bool hasObservedInputs;
        private bool failureReported;

        public BoardCameraSystem(IBoardCameraView view)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            if (view.TargetCamera == null || view.Board == null || view.BoardOrigin == null)
            {
                throw new InvalidOperationException(
                    "BoardCameraView requires a Camera and a BoardView with a BoardDefinition.");
            }
        }

        public void Start()
        {
            hasObservedInputs = false;
            FrameAndObserve();
        }

        public void LateTick()
        {
            if (InputsChanged())
            {
                FrameAndObserve();
            }
        }

        public bool FrameNow()
        {
            if (!TryCalculatePose(out Vector3 position, out Quaternion rotation))
            {
                if (!failureReported)
                {
                    Debug.LogWarning(InvalidFramingWarning, view.LogContext);
                    failureReported = true;
                }

                return false;
            }

            failureReported = false;
            view.ApplyPose(position, rotation);
            return true;
        }

        public bool TryCalculatePosition(out Vector3 position)
        {
            return TryCalculatePose(out position, out _);
        }

        public bool TryCalculatePose(
            out Vector3 position,
            out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;

            Camera targetCamera = view.TargetCamera;
            BoardDefinition board = view.Board;
            Transform boardOrigin = view.BoardOrigin;
            if (targetCamera == null || board == null || boardOrigin == null
                || targetCamera.orthographic
                || !IsFinite(view.CameraLocalPositionOffset)
                || !TryGetFramingRotation(out rotation))
            {
                return false;
            }

            if (!BoardCameraFramingPlane.TryCreate(
                    board,
                    boardOrigin,
                    view.EdgePaddingCells,
                    board.MaxCameraGridXSpan,
                    board.MaxCameraGridYSpan,
                    out BoardCameraFramingPlane plane))
            {
                return false;
            }

            Rect framingRect = view.CompositionRectInSafeArea;
            if (view.UseRuntimeSafeArea
                && !TryBuildSafeViewportRect(
                    targetCamera.pixelRect,
                    view.ScreenSafeArea,
                    view.CompositionRectInSafeArea,
                    out framingRect))
            {
                return false;
            }

            if (!BoardCameraFramingSolver.TryCalculatePosition(
                    plane,
                    rotation,
                    targetCamera.fieldOfView,
                    targetCamera.aspect,
                    targetCamera.nearClipPlane,
                    framingRect,
                    out Vector3 fittedPosition))
            {
                return false;
            }

            position = fittedPosition + rotation * view.CameraLocalPositionOffset;
            return IsFinite(position);
        }

        public static bool TryBuildSafeViewportRect(
            Rect cameraPixelRect,
            Rect screenSafeArea,
            Rect compositionRectInSafeArea,
            out Rect safeViewportRect)
        {
            safeViewportRect = default;
            if (!IsValidPixelRect(cameraPixelRect)
                || !IsValidPixelRect(screenSafeArea)
                || !IsValidNormalizedRect(compositionRectInSafeArea))
            {
                return false;
            }

            float intersectionMinX = Mathf.Max(cameraPixelRect.xMin, screenSafeArea.xMin);
            float intersectionMinY = Mathf.Max(cameraPixelRect.yMin, screenSafeArea.yMin);
            float intersectionMaxX = Mathf.Min(cameraPixelRect.xMax, screenSafeArea.xMax);
            float intersectionMaxY = Mathf.Min(cameraPixelRect.yMax, screenSafeArea.yMax);
            if (intersectionMaxX <= intersectionMinX || intersectionMaxY <= intersectionMinY)
            {
                return false;
            }

            var safeAreaInCamera = new Rect(
                (intersectionMinX - cameraPixelRect.xMin) / cameraPixelRect.width,
                (intersectionMinY - cameraPixelRect.yMin) / cameraPixelRect.height,
                (intersectionMaxX - intersectionMinX) / cameraPixelRect.width,
                (intersectionMaxY - intersectionMinY) / cameraPixelRect.height);
            safeViewportRect = new Rect(
                safeAreaInCamera.xMin
                    + compositionRectInSafeArea.xMin * safeAreaInCamera.width,
                safeAreaInCamera.yMin
                    + compositionRectInSafeArea.yMin * safeAreaInCamera.height,
                compositionRectInSafeArea.width * safeAreaInCamera.width,
                compositionRectInSafeArea.height * safeAreaInCamera.height);
            return IsValidNormalizedRect(safeViewportRect);
        }

        private void FrameAndObserve()
        {
            FrameNow();
            ObserveInputs();
        }

        private bool InputsChanged()
        {
            if (!hasObservedInputs
                || observedPadding != view.EdgePaddingCells
                || observedCompositionRect != view.CompositionRectInSafeArea
                || observedCameraLocalPositionOffset != view.CameraLocalPositionOffset
                || observedCameraLocalRotationOffsetEuler != view.CameraLocalRotationOffsetEuler
                || observedAuthoredBaseRotationEuler != view.AuthoredBaseRotationEuler)
            {
                return true;
            }

            Camera camera = view.TargetCamera;
            BoardDefinition board = view.Board;
            Transform boardOrigin = view.BoardOrigin;
            if (camera == null || board == null || boardOrigin == null)
            {
                return true;
            }

            return observedBoard != board
                || observedMaxCameraGridXSpan != board.MaxCameraGridXSpan
                || observedMaxCameraGridYSpan != board.MaxCameraGridYSpan
                || observedPixelRect != camera.pixelRect
                || observedSafeArea != view.ScreenSafeArea
                || observedAspect != camera.aspect
                || observedFieldOfView != camera.fieldOfView
                || observedNearClip != camera.nearClipPlane
                || observedOrthographic != camera.orthographic
                || observedCameraRotation != camera.transform.rotation
                || observedBoardPosition != boardOrigin.position
                || observedBoardRotation != boardOrigin.rotation
                || observedBoardScale != boardOrigin.lossyScale;
        }

        private void ObserveInputs()
        {
            observedBoard = view.Board;
            observedMaxCameraGridXSpan = observedBoard != null
                ? observedBoard.MaxCameraGridXSpan
                : 0;
            observedMaxCameraGridYSpan = observedBoard != null
                ? observedBoard.MaxCameraGridYSpan
                : 0;
            observedPadding = view.EdgePaddingCells;
            observedCompositionRect = view.CompositionRectInSafeArea;
            observedCameraLocalPositionOffset = view.CameraLocalPositionOffset;
            observedCameraLocalRotationOffsetEuler = view.CameraLocalRotationOffsetEuler;
            observedAuthoredBaseRotationEuler = view.AuthoredBaseRotationEuler;
            observedSafeArea = view.ScreenSafeArea;

            Camera camera = view.TargetCamera;
            if (camera != null)
            {
                observedPixelRect = camera.pixelRect;
                observedAspect = camera.aspect;
                observedFieldOfView = camera.fieldOfView;
                observedNearClip = camera.nearClipPlane;
                observedOrthographic = camera.orthographic;
                observedCameraRotation = camera.transform.rotation;
            }

            Transform boardOrigin = view.BoardOrigin;
            if (boardOrigin != null)
            {
                observedBoardPosition = boardOrigin.position;
                observedBoardRotation = boardOrigin.rotation;
                observedBoardScale = boardOrigin.lossyScale;
            }

            hasObservedInputs = true;
        }

        private bool EnsureAuthoredBaseRotation()
        {
            if (!view.HasAuthoredBaseRotation || !IsFinite(view.AuthoredBaseRotationEuler))
            {
                view.CaptureCurrentCameraRotationAsBase();
            }

            return view.HasAuthoredBaseRotation && IsFinite(view.AuthoredBaseRotationEuler);
        }

        private bool TryGetFramingRotation(out Quaternion rotation)
        {
            rotation = Quaternion.identity;
            if (!EnsureAuthoredBaseRotation()
                || !IsFinite(view.CameraLocalRotationOffsetEuler))
            {
                return false;
            }

            rotation = Quaternion.Euler(view.AuthoredBaseRotationEuler)
                * Quaternion.Euler(view.CameraLocalRotationOffsetEuler);
            return IsFinite(rotation);
        }

        private static bool IsValidPixelRect(Rect rect) =>
            IsFinite(rect.xMin) && IsFinite(rect.yMin)
            && IsFinite(rect.width) && IsFinite(rect.height)
            && rect.width > 0f && rect.height > 0f;

        private static bool IsValidNormalizedRect(Rect rect) =>
            IsValidPixelRect(rect)
            && rect.xMin >= 0f && rect.yMin >= 0f
            && rect.xMax <= 1f && rect.yMax <= 1f;

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(Quaternion value) =>
            IsFinite(value.x) && IsFinite(value.y)
            && IsFinite(value.z) && IsFinite(value.w);
    }
}
