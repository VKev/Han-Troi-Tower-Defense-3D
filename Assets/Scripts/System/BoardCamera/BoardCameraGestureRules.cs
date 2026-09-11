using UnityEngine;

namespace TowerDefense3D.GridPlacement
{
    /// <summary>
    /// Authoring limits for the player-driven zoom and pan applied on top of the board framing.
    /// </summary>
    [CreateAssetMenu(
        fileName = "BoardCameraGestureRules",
        menuName = "Tower Defense/Camera/Gesture Rules")]
    public sealed class BoardCameraGestureRules : ScriptableObject
    {
        [Header("Limits")]
        [Tooltip("How far the camera may travel along its own forward axis at full zoom in. "
            + "Zoom out is not authored: zero offset is the computed board framing, which is "
            + "as far out as the player can ever get.")]
        [SerializeField, Min(0f)] private float maxZoomInDistanceMeters = 5f;

        [Tooltip("How far the camera may slide across its own view plane while fully zoomed in. "
            + "The allowance shrinks with the zoom, so zooming back out re-centres the board.")]
        [SerializeField, Min(0f)] private float maxPanDistanceMeters = 4f;

        [Header("Zoom Sensitivity")]
        [Tooltip("Zoom gained when two fingers spread apart by one full screen height. "
            + "At 2 a half-screen spread covers the whole zoom range.")]
        [SerializeField, Min(0f)] private float pinchZoomPerScreenHeight = 2f;

        [Tooltip("Zoom gained per mouse wheel notch, for testing in the Editor.")]
        [SerializeField, Range(0f, 1f)] private float scrollZoomPerNotch = 0.08f;

        [Tooltip("How quickly the applied zoom chases the requested zoom. Higher is snappier.")]
        [SerializeField, Min(0.01f)] private float zoomSmoothingPerSecond = 14f;

        [Header("Pan Sensitivity")]
        [Tooltip("Camera travel produced by dragging across one full screen height.")]
        [SerializeField, Min(0f)] private float panMetersPerScreenHeight = 8f;

        [Tooltip("How far the pointer must move before a press on empty ground becomes a pan. "
            + "Below this a press stays an ordinary tap, so tapping bare ground still dismisses "
            + "the selected tower.")]
        [SerializeField, Min(0f)] private float panDragThresholdPixels = 24f;

        public float MaxZoomInDistanceMeters => maxZoomInDistanceMeters;
        public float MaxPanDistanceMeters => maxPanDistanceMeters;
        public float PinchZoomPerScreenHeight => pinchZoomPerScreenHeight;
        public float ScrollZoomPerNotch => scrollZoomPerNotch;
        public float ZoomSmoothingPerSecond => zoomSmoothingPerSecond;
        public float PanMetersPerScreenHeight => panMetersPerScreenHeight;
        public float PanDragThresholdPixels => panDragThresholdPixels;
    }
}
