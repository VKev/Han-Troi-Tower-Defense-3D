using System.Collections.Generic;
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

        // Reused so the per-frame hit test does not allocate.
        private static readonly List<RaycastResult> UiHits = new List<RaycastResult>();
        private static PointerEventData uiProbe;

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
                        IsPointerOverUi(touch.position.ReadValue()));
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
                        IsPointerOverUi(mouse.position.ReadValue()));
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

        /// <summary>
        /// Whether a tap at this position lands on UI rather than on the board.
        /// </summary>
        /// <remarks>
        /// Raycast directly rather than asking EventSystem.IsPointerOverGameObject, which reports
        /// what the event system resolved on the *previous* frame. A touch that began this frame
        /// has no previous frame, so that call answers false for the one frame that matters - the
        /// press - and gameplay then treats a tap on a button as a tap on bare ground. That is
        /// what made the Sell button look dead: pressing it cleared the tower selection the sale
        /// needed, while Unlink, sitting closer to the tower, still fell inside the pick radius
        /// and re-selected it instead.
        /// </remarks>
        private static bool IsPointerOverUi(Vector2 screenPosition)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            if (uiProbe == null)
            {
                uiProbe = new PointerEventData(eventSystem);
            }

            uiProbe.position = screenPosition;
            UiHits.Clear();
            eventSystem.RaycastAll(uiProbe, UiHits);
            return UiHits.Count > 0;
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
