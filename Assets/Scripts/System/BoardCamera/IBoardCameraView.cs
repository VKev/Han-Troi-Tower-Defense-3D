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
        Vector3 CameraLocalPositionOffset { get; }
        Vector3 CameraLocalRotationOffsetEuler { get; }
        Vector3 AuthoredBaseRotationEuler { get; }
        bool HasAuthoredBaseRotation { get; }
        bool UseRuntimeSafeArea { get; }
        Rect ScreenSafeArea { get; }
        Object LogContext { get; }

        void CaptureCurrentCameraRotationAsBase();
        void ApplyPose(Vector3 position, Quaternion rotation);
    }
}
