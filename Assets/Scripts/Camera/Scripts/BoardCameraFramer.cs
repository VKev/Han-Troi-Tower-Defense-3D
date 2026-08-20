using UnityEngine;

namespace TowerDefense3D.GridPlacement
{
    [DisallowMultipleComponent]
    public sealed class BoardCameraFramer : MonoBehaviour
    {
        private static readonly Rect DefaultCompositionRect =
            new Rect(0.05f, 0.08f, 0.9f, 0.84f);
        private const float PositionEpsilonSquared = 0.000001f;
        private const string InvalidFramingWarning = "Board camera framing skipped because its Camera, Board, perspective lens, or playable Board footprint is invalid.";

        [SerializeField] private Camera targetCamera;
        [SerializeField] private BoardScenePresenter boardPresenter;
        [SerializeField, Min(0f)] private float edgePaddingCells = 1f;
        [SerializeField] private Rect compositionRectInSafeArea =
            new Rect(0.05f, 0.08f, 0.9f, 0.84f);

        private Camera observedCamera;
        private BoardScenePresenter observedPresenter;
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
        private Vector3 observedBoardPosition;
        private Quaternion observedBoardRotation;
        private Vector3 observedBoardScale;
        private Quaternion observedCameraRotation;
        private bool hasObservedInputs;
        private bool failureReported;

        public Camera TargetCamera => targetCamera;
        public BoardScenePresenter BoardPresenter => boardPresenter;

        private void Reset()
        {
            targetCamera = GetComponent<Camera>();
        }

        private void OnEnable()
        {
            hasObservedInputs = false;
            if (Application.isPlaying)
            {
                FrameAndObserve();
            }
        }

        private void LateUpdate()
        {
            if (Application.isPlaying && InputsChanged())
            {
                FrameAndObserve();
            }
        }

        private void OnValidate()
        {
            edgePaddingCells = Mathf.Max(0f, edgePaddingCells);
            compositionRectInSafeArea = SanitizeCompositionRect(
                compositionRectInSafeArea);
            hasObservedInputs = false;
        }

        public bool FrameNow()
        {
            if (!TryCalculatePosition(out Vector3 position))
            {
                if (!failureReported)
                {
                    Debug.LogWarning(
                        InvalidFramingWarning,
                        this);
                    failureReported = true;
                }

                return false;
            }

            failureReported = false;
            Transform cameraTransform = targetCamera.transform;
            if ((cameraTransform.position - position).sqrMagnitude
                > PositionEpsilonSquared)
            {
                cameraTransform.position = position;
            }

            return true;
        }

        public bool TryCalculatePosition(out Vector3 position)
        {
            position = default;
            if (targetCamera == null || boardPresenter == null
                || boardPresenter.Board == null || targetCamera.orthographic)
            {
                return false;
            }

            BoardDefinition board = boardPresenter.Board;
            if (!BoardCameraFramingPlane.TryCreate(
                    board,
                    boardPresenter.transform,
                    edgePaddingCells,
                    board.MaxCameraGridXSpan,
                    board.MaxCameraGridYSpan,
                    out BoardCameraFramingPlane plane))
            {
                return false;
            }

            Rect framingRect = compositionRectInSafeArea;
            if (Application.isPlaying
                && !TryBuildSafeViewportRect(
                    targetCamera.pixelRect,
                    Screen.safeArea,
                    compositionRectInSafeArea,
                    out framingRect))
            {
                return false;
            }

            return BoardCameraFramingSolver.TryCalculatePosition(
                plane,
                targetCamera.transform.rotation,
                targetCamera.fieldOfView,
                targetCamera.aspect,
                targetCamera.nearClipPlane,
                framingRect,
                out position);
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

            float intersectionMinX = Mathf.Max(
                cameraPixelRect.xMin,
                screenSafeArea.xMin);
            float intersectionMinY = Mathf.Max(
                cameraPixelRect.yMin,
                screenSafeArea.yMin);
            float intersectionMaxX = Mathf.Min(
                cameraPixelRect.xMax,
                screenSafeArea.xMax);
            float intersectionMaxY = Mathf.Min(
                cameraPixelRect.yMax,
                screenSafeArea.yMax);
            if (intersectionMaxX <= intersectionMinX
                || intersectionMaxY <= intersectionMinY)
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
                || observedCamera != targetCamera
                || observedPresenter != boardPresenter
                || observedPadding != edgePaddingCells
                || observedCompositionRect != compositionRectInSafeArea)
            {
                return true;
            }

            BoardDefinition currentBoard = boardPresenter != null
                ? boardPresenter.Board
                : null;
            if (observedBoard != currentBoard || targetCamera == null
                || (currentBoard != null
                    && (observedMaxCameraGridXSpan != currentBoard.MaxCameraGridXSpan
                        || observedMaxCameraGridYSpan != currentBoard.MaxCameraGridYSpan))
                || observedPixelRect != targetCamera.pixelRect
                || observedSafeArea != Screen.safeArea
                || observedAspect != targetCamera.aspect
                || observedFieldOfView != targetCamera.fieldOfView
                || observedNearClip != targetCamera.nearClipPlane
                || observedOrthographic != targetCamera.orthographic
                || observedCameraRotation != targetCamera.transform.rotation)
            {
                return true;
            }

            if (boardPresenter == null)
            {
                return false;
            }

            Transform boardTransform = boardPresenter.transform;
            return observedBoardPosition != boardTransform.position
                || observedBoardRotation != boardTransform.rotation
                || observedBoardScale != boardTransform.lossyScale;
        }

        private void ObserveInputs()
        {
            observedCamera = targetCamera;
            observedPresenter = boardPresenter;
            observedBoard = boardPresenter != null ? boardPresenter.Board : null;
            observedMaxCameraGridXSpan = observedBoard != null
                ? observedBoard.MaxCameraGridXSpan
                : 0;
            observedMaxCameraGridYSpan = observedBoard != null
                ? observedBoard.MaxCameraGridYSpan
                : 0;
            observedPadding = edgePaddingCells;
            observedCompositionRect = compositionRectInSafeArea;
            observedSafeArea = Screen.safeArea;

            if (targetCamera != null)
            {
                observedPixelRect = targetCamera.pixelRect;
                observedAspect = targetCamera.aspect;
                observedFieldOfView = targetCamera.fieldOfView;
                observedNearClip = targetCamera.nearClipPlane;
                observedOrthographic = targetCamera.orthographic;
                observedCameraRotation = targetCamera.transform.rotation;
            }

            if (boardPresenter != null)
            {
                Transform boardTransform = boardPresenter.transform;
                observedBoardPosition = boardTransform.position;
                observedBoardRotation = boardTransform.rotation;
                observedBoardScale = boardTransform.lossyScale;
            }

            hasObservedInputs = true;
        }

        private static Rect SanitizeCompositionRect(Rect rect)
        {
            float minX = Mathf.Clamp01(rect.xMin);
            float minY = Mathf.Clamp01(rect.yMin);
            float maxX = Mathf.Clamp01(rect.xMax);
            float maxY = Mathf.Clamp01(rect.yMax);
            if (maxX <= minX || maxY <= minY)
            {
                return DefaultCompositionRect;
            }

            return Rect.MinMaxRect(minX, minY, maxX, maxY);
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
    }
}
