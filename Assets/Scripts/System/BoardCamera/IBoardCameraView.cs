using UnityEngine;

namespace TowerDefense3D.GridPlacement
{
    public interface IBoardCameraView
    {
        Camera TargetCamera { get; }
        BoardDefinition Board { get; }
        Transform BoardOrigin { get; }
        float EdgePaddingCells { get; }
        Rect CompositionRectInSafeArea { get; }
        Vector3 AuthoredBaseRotationEuler { get; }
        bool HasAuthoredBaseRotation { get; }
        bool UseRuntimeSafeArea { get; }
        Rect ScreenSafeArea { get; }
        Object LogContext { get; }

        /// <summary>
        /// Player-driven camera-local offset folded into the framing pose: x and y slide the view
        /// across its own plane, z pulls it toward the board.
        /// </summary>
        /// <remarks>
        /// Held here rather than written straight onto the camera transform because the framing
        /// pose is recomputed from scratch whenever the viewport, safe area or lens changes. A
        /// direct transform write survives an idle frame but is discarded by the next reframe, so
        /// a device rotation or a Device Simulator swap would snap the board back to full view in
        /// the middle of a gesture. Routed through the framing inputs, the offset is re-applied on
        /// top of every recomputed pose instead.
        /// </remarks>
        Vector3 ViewerPositionOffset { get; }

        void SetViewerPositionOffset(Vector3 offset);
        void CaptureCurrentCameraRotationAsBase();
        void ApplyPose(Vector3 position, Quaternion rotation);
    }
}
