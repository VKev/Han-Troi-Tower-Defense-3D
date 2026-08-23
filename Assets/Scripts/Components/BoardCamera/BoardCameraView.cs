using UnityEngine;

namespace TowerDefense3D.GridPlacement
{
    [DisallowMultipleComponent]
    public sealed class BoardCameraView : MonoBehaviour, IBoardCameraView
    {
        private static readonly Rect DefaultCompositionRect =
            new Rect(0.05f, 0.08f, 0.9f, 0.84f);
        private const float PositionEpsilonSquared = 0.000001f;

        [SerializeField] private Camera targetCamera;
        [SerializeField] private BoardView boardView;
        [SerializeField, Min(0f)] private float edgePaddingCells = 1f;
        [SerializeField] private Rect compositionRectInSafeArea =
            new Rect(0.05f, 0.08f, 0.9f, 0.84f);
        [Tooltip("Applied after automatic framing in the Camera's local right, up, and forward axes.")]
        [SerializeField] private Vector3 cameraLocalPositionOffset;
        [Tooltip("Local Euler delta applied to the captured authored Camera rotation before framing.")]
        [SerializeField] private Vector3 cameraLocalRotationOffsetEuler;
        [SerializeField, HideInInspector] private Vector3 authoredBaseRotationEuler;
        [SerializeField, HideInInspector] private bool hasAuthoredBaseRotation;

        public Camera TargetCamera => targetCamera;
        public BoardView BoardView => boardView;
        public BoardDefinition Board => boardView != null ? boardView.Board : null;
        public Transform BoardOrigin => boardView != null ? boardView.transform : null;
        public float EdgePaddingCells => edgePaddingCells;
        public Rect CompositionRectInSafeArea => compositionRectInSafeArea;
        public Vector3 CameraLocalPositionOffset => cameraLocalPositionOffset;
        public Vector3 CameraLocalRotationOffsetEuler => cameraLocalRotationOffsetEuler;
        public Vector3 AuthoredBaseRotationEuler => authoredBaseRotationEuler;
        public bool HasAuthoredBaseRotation => hasAuthoredBaseRotation;
        public bool UseRuntimeSafeArea => Application.isPlaying;
        public Rect ScreenSafeArea => Screen.safeArea;
        public Object LogContext => this;

        private void Reset()
        {
            targetCamera = GetComponent<Camera>();
            CaptureCurrentCameraRotationAsBase();
        }

        private void OnValidate()
        {
            edgePaddingCells = Mathf.Max(0f, edgePaddingCells);
            compositionRectInSafeArea = SanitizeCompositionRect(compositionRectInSafeArea);
            cameraLocalPositionOffset = SanitizeVector(cameraLocalPositionOffset);
            cameraLocalRotationOffsetEuler = SanitizeVector(cameraLocalRotationOffsetEuler);

            if (!hasAuthoredBaseRotation || !IsFinite(authoredBaseRotationEuler))
            {
                CaptureCurrentCameraRotationAsBase();
            }
        }

        [ContextMenu("Capture Current Camera Rotation As Base")]
        public void CaptureCurrentCameraRotationAsBase()
        {
            if (targetCamera == null)
            {
                return;
            }

            authoredBaseRotationEuler = targetCamera.transform.rotation.eulerAngles;
            hasAuthoredBaseRotation = true;
        }

        public void ApplyPose(Vector3 position, Quaternion rotation)
        {
            Transform cameraTransform = targetCamera.transform;
            if ((cameraTransform.position - position).sqrMagnitude <= PositionEpsilonSquared
                && Quaternion.Angle(cameraTransform.rotation, rotation) <= 0.0001f)
            {
                return;
            }

            cameraTransform.SetPositionAndRotation(position, rotation);
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

        private static Vector3 SanitizeVector(Vector3 value) =>
            new Vector3(
                IsFinite(value.x) ? value.x : 0f,
                IsFinite(value.y) ? value.y : 0f,
                IsFinite(value.z) ? value.z : 0f);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }
}
