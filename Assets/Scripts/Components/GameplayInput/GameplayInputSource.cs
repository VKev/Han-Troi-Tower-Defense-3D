using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TowerDefense3D.GameplayInput
{
    /// <summary>
    /// Touch-first Unity Input System boundary for one level scene.
    /// </summary>
    public sealed class GameplayInputSource : MonoBehaviour, IGameplayInputSource
    {
        private const int MousePointerId = -1;

        private bool wasInterrupted;

        public GameplayInputSnapshot Capture()
        {
            bool cancelRequested = Keyboard.current != null
                && Keyboard.current.escapeKey.wasPressedThisFrame;
            bool interrupted = ConsumeInterruption();

            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                var touch = touchscreen.primaryTouch;
                bool wasPressed = touch.press.wasPressedThisFrame;
                bool isPressed = touch.press.isPressed;
                bool wasReleased = touch.press.wasReleasedThisFrame;
                if (wasPressed || isPressed || wasReleased)
                {
                    int pointerId = touch.touchId.ReadValue();
                    return CreatePointerSnapshot(
                        cancelRequested,
                        interrupted,
                        wasPressed,
                        isPressed,
                        wasReleased,
                        pointerId,
                        touch.position.ReadValue(),
                        IsPointerOverUi(pointerId));
                }
            }

            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                bool wasPressed = mouse.leftButton.wasPressedThisFrame;
                bool isPressed = mouse.leftButton.isPressed;
                bool wasReleased = mouse.leftButton.wasReleasedThisFrame;
                if (wasPressed || isPressed || wasReleased)
                {
                    return CreatePointerSnapshot(
                        cancelRequested,
                        interrupted,
                        wasPressed,
                        isPressed,
                        wasReleased,
                        MousePointerId,
                        mouse.position.ReadValue(),
                        IsPointerOverUi(MousePointerId));
                }
            }

            return new GameplayInputSnapshot(
                cancelRequested,
                interrupted,
                hasPointerInput: false,
                wasPressed: false,
                isPressed: false,
                wasReleased: false,
                pointerId: 0,
                screenPosition: default,
                isPointerOverUi: false);
        }

        private static GameplayInputSnapshot CreatePointerSnapshot(
            bool cancelRequested,
            bool wasInterrupted,
            bool wasPressed,
            bool isPressed,
            bool wasReleased,
            int pointerId,
            Vector2 screenPosition,
            bool isPointerOverUi)
        {
            return new GameplayInputSnapshot(
                cancelRequested,
                wasInterrupted,
                hasPointerInput: true,
                wasPressed,
                isPressed,
                wasReleased,
                pointerId,
                screenPosition,
                isPointerOverUi);
        }

        private static bool IsPointerOverUi(int pointerId)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            return pointerId == MousePointerId
                ? eventSystem.IsPointerOverGameObject()
                : eventSystem.IsPointerOverGameObject(pointerId);
        }

        private bool ConsumeInterruption()
        {
            bool interrupted = wasInterrupted;
            wasInterrupted = false;
            return interrupted;
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                wasInterrupted = true;
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                wasInterrupted = true;
            }
        }
    }
}
