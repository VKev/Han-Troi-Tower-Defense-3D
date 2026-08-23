using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TowerDefense3D.Towers
{
    [DisallowMultipleComponent]
    public sealed class TowerNetworkInputController : MonoBehaviour
    {
        private enum PointerKind
        {
            None,
            Touch,
            Mouse
        }

        [SerializeField, Min(24f)] private float selectionRadiusPixels = 96f;
        [SerializeField, Min(4f)] private float linkDragThresholdPixels = 24f;

        private ITowerNetworkSceneRegistry registry;
        private Camera worldCamera;
        private Action cancelPlacement;
        private PointerKind pointerKind;
        private Vector2 pressPosition;
        private Vector2 currentPosition;
        private TowerRuntimeView pressedTower;
        private TowerRuntimeView previewTarget;

        public event Action SelectionChanged;
        public event Action FeedbackChanged;

        public bool IsInitialized => registry != null;
        public TowerRuntimeView SelectedTower { get; private set; }
        public bool IsDraggingLink { get; private set; }
        public TowerRuntimeView PreviewTarget => previewTarget;
        public Vector3 PreviewWorldPosition => GetPreviewWorldPosition();
        public string LastFeedback { get; private set; } = string.Empty;

        public void Initialize(
            ITowerNetworkSceneRegistry sceneRegistry,
            Camera sceneCamera,
            Action cancelPlacementAction)
        {
            registry = sceneRegistry ?? throw new ArgumentNullException(nameof(sceneRegistry));
            worldCamera = sceneCamera != null
                ? sceneCamera
                : throw new ArgumentNullException(nameof(sceneCamera));
            cancelPlacement = cancelPlacementAction ?? throw new ArgumentNullException(nameof(cancelPlacementAction));
            ResetPointer();
            ClearSelection();
            SetFeedback(string.Empty);
        }

        public void Shutdown()
        {
            ResetPointer();
            ClearSelection();
            SetFeedback(string.Empty);
            cancelPlacement = null;
            worldCamera = null;
            registry = null;
        }

        public void ClearSelection()
        {
            if (SelectedTower == null)
            {
                return;
            }

            SelectedTower = null;
            SelectionChanged?.Invoke();
        }

        public void Select(TowerRuntimeView tower)
        {
            TowerRuntimeView nextSelection = tower != null && tower.IsRegistered
                ? tower
                : null;
            if (SelectedTower == nextSelection)
            {
                return;
            }

            SelectedTower = nextSelection;
            SelectionChanged?.Invoke();
        }

        public void ReportFeedback(string message)
        {
            SetFeedback(message);
        }

        private void Update()
        {
            if (!IsInitialized)
            {
                return;
            }

            bool touchHandled = HandlePrimaryTouch();

#if UNITY_EDITOR
            if (!touchHandled)
            {
                HandleEditorMouse();
            }
#endif
        }

        private bool HandlePrimaryTouch()
        {
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen == null)
            {
                return false;
            }

            var touch = touchscreen.primaryTouch;
            bool ownsTouch = touch.press.isPressed
                || touch.press.wasPressedThisFrame
                || touch.press.wasReleasedThisFrame
                || pointerKind == PointerKind.Touch;
            if (!ownsTouch)
            {
                return false;
            }

            Vector2 position = touch.position.ReadValue();
            int pointerId = touch.touchId.ReadValue();
            if (touch.press.wasPressedThisFrame && pointerKind == PointerKind.None)
            {
                BeginPointer(PointerKind.Touch, pointerId, position);
            }

            if (pointerKind == PointerKind.Touch && touch.press.isPressed)
            {
                MovePointer(position);
            }

            if (pointerKind == PointerKind.Touch && touch.press.wasReleasedThisFrame)
            {
                EndPointer(position);
            }

            return true;
        }

#if UNITY_EDITOR
        private void HandleEditorMouse()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || pointerKind == PointerKind.Touch)
            {
                return;
            }

            Vector2 position = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame && pointerKind == PointerKind.None)
            {
                BeginPointer(PointerKind.Mouse, -1, position);
            }

            if (pointerKind == PointerKind.Mouse && mouse.leftButton.isPressed)
            {
                MovePointer(position);
            }

            if (pointerKind == PointerKind.Mouse && mouse.leftButton.wasReleasedThisFrame)
            {
                EndPointer(position);
            }
        }
#endif

        private void BeginPointer(PointerKind kind, int pointerId, Vector2 screenPosition)
        {
            pointerKind = kind;
            pressPosition = screenPosition;
            currentPosition = screenPosition;
            IsDraggingLink = false;
            previewTarget = null;

            if (PointerStartedOverUi(kind, pointerId) || !TryPickTower(screenPosition, out pressedTower))
            {
                pressedTower = null;
                return;
            }

            cancelPlacement();
            Select(pressedTower);
            SetFeedback($"Selected {GetDisplayName(pressedTower)}.");
        }

        private void MovePointer(Vector2 screenPosition)
        {
            currentPosition = screenPosition;
            if (pressedTower == null)
            {
                return;
            }

            float dragThresholdSquared = linkDragThresholdPixels * linkDragThresholdPixels;
            if (!IsDraggingLink && (screenPosition - pressPosition).sqrMagnitude >= dragThresholdSquared)
            {
                IsDraggingLink = true;
            }

            previewTarget = IsDraggingLink && TryPickTower(screenPosition, out TowerRuntimeView target)
                && target != pressedTower
                    ? target
                    : null;
        }

        private void EndPointer(Vector2 screenPosition)
        {
            MovePointer(screenPosition);
            if (pressedTower != null && IsDraggingLink)
            {
                CompleteLinkGesture();
            }

            ResetPointer();
        }

        private void CompleteLinkGesture()
        {
            if (previewTarget == null)
            {
                SetFeedback("Link cancelled: release over another tower.");
                return;
            }

            if (registry.TryRewire(pressedTower, previewTarget, out string error))
            {
                SetFeedback($"Linked {GetDisplayName(pressedTower)} to {GetDisplayName(previewTarget)}.");
            }
            else
            {
                SetFeedback(error);
            }
        }

        private bool TryPickTower(Vector2 screenPosition, out TowerRuntimeView closestTower)
        {
            IReadOnlyList<TowerRuntimeView> towers = registry.CreateTowerViewSnapshot();
            float closestDistanceSquared = selectionRadiusPixels * selectionRadiusPixels;
            closestTower = null;

            for (int index = 0; index < towers.Count; index++)
            {
                TowerRuntimeView tower = towers[index];
                if (tower == null || !tower.IsRegistered)
                {
                    continue;
                }

                Vector3 towerScreenPosition = worldCamera.WorldToScreenPoint(tower.PresentationAnchor);
                if (towerScreenPosition.z <= 0f)
                {
                    continue;
                }

                float distanceSquared = ((Vector2)towerScreenPosition - screenPosition).sqrMagnitude;
                if (distanceSquared <= closestDistanceSquared)
                {
                    closestDistanceSquared = distanceSquared;
                    closestTower = tower;
                }
            }

            return closestTower != null;
        }

        private Vector3 GetPreviewWorldPosition()
        {
            if (previewTarget != null)
            {
                return previewTarget.PresentationAnchor;
            }

            if (worldCamera == null || pressedTower == null)
            {
                return Vector3.zero;
            }

            float depth = worldCamera.WorldToScreenPoint(pressedTower.PresentationAnchor).z;
            return worldCamera.ScreenToWorldPoint(new Vector3(currentPosition.x, currentPosition.y, depth));
        }

        private bool PointerStartedOverUi(PointerKind kind, int pointerId)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            return kind == PointerKind.Mouse
                ? eventSystem.IsPointerOverGameObject()
                : eventSystem.IsPointerOverGameObject(pointerId);
        }

        private void ResetPointer()
        {
            pointerKind = PointerKind.None;
            pressPosition = default;
            currentPosition = default;
            pressedTower = null;
            previewTarget = null;
            IsDraggingLink = false;
        }

        private void SetFeedback(string message)
        {
            string normalized = message ?? string.Empty;
            if (string.Equals(LastFeedback, normalized, StringComparison.Ordinal))
            {
                return;
            }

            LastFeedback = normalized;
            FeedbackChanged?.Invoke();
        }

        private static string GetDisplayName(TowerRuntimeView tower)
        {
            string displayName = tower?.CombatDefinition?.Core?.DisplayName;
            return string.IsNullOrWhiteSpace(displayName) ? "Tower" : displayName;
        }
    }
}
