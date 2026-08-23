using UnityEngine;

namespace TowerDefense3D.GameplayInput
{
    /// <summary>
    /// Immutable gameplay input captured once for the current frame.
    /// </summary>
    public readonly struct GameplayInputSnapshot
    {
        public GameplayInputSnapshot(
            bool cancelRequested,
            bool wasInterrupted,
            bool hasPointerInput,
            bool wasPressed,
            bool isPressed,
            bool wasReleased,
            int pointerId,
            Vector2 screenPosition,
            bool isPointerOverUi)
        {
            CancelRequested = cancelRequested;
            WasInterrupted = wasInterrupted;
            HasPointerInput = hasPointerInput;
            WasPressed = wasPressed;
            IsPressed = isPressed;
            WasReleased = wasReleased;
            PointerId = pointerId;
            ScreenPosition = screenPosition;
            IsPointerOverUi = isPointerOverUi;
        }

        public bool CancelRequested { get; }
        public bool WasInterrupted { get; }
        public bool HasPointerInput { get; }
        public bool WasPressed { get; }
        public bool IsPressed { get; }
        public bool WasReleased { get; }
        public int PointerId { get; }
        public Vector2 ScreenPosition { get; }
        public bool IsPointerOverUi { get; }
    }
}
