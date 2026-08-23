using System;
using TowerDefense3D.Towers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TowerDefense3D.GridPlacement
{
    /// <summary>
    /// Scene-scoped owner for one mobile placement interaction.
    /// </summary>
    public sealed class GridPlacementPresenter : MonoBehaviour
    {
        private enum PointerState
        {
            Idle,
            Tracking,
            IgnoredUntilRelease
        }

        private enum PointerKind
        {
            None,
            Touch,
            Mouse,
            UiDrag
        }

        [Header("Board")]
        [SerializeField] private BoardDefinition boardDefinition;
        [SerializeField] private Transform boardOrigin;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private LayerMask placementSurfaceMask = ~0;
        [SerializeField, Min(1f)] private float maxRayDistance = 500f;

        [Header("Placement")]
        [SerializeField] private GridPlacementView view;
        [SerializeField] private Transform placedObjectsRoot;
        [SerializeField] private TowerDefinition initialTower;

        private GridPlacementService service;
        private TowerDefinition selectedTower;
        private TowerCombatDefinition selectedCombatDefinition;
        private PointerState pointerState;
        private PointerKind pointerKind;
        private int trackedPointerId;
        private bool hasCandidate;
        private bool candidateIsValid;
        private GridCell candidateCell;

        public TowerDefinition SelectedTower => selectedTower;
        public TowerCombatDefinition SelectedCombatDefinition => selectedCombatDefinition;
        public Camera WorldCamera => worldCamera;
        public bool IsPlacementActive => selectedTower != null;
        public bool HasCandidate => hasCandidate;
        public bool CandidateIsValid => hasCandidate && candidateIsValid;
        public GridCell CandidateCell => candidateCell;
        public GridOccupancy Occupancy => service?.Occupancy;
        public bool IsInitialized { get; private set; }

        public event Action<TowerPlacementRecord> TowerPlaced;

        public void Initialize()
        {
            if (IsInitialized)
            {
                return;
            }

            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            if (boardDefinition == null)
            {
                throw new InvalidOperationException("GridPlacementPresenter requires a BoardDefinition.");
            }

            try
            {
                Vector3 origin = boardOrigin != null ? boardOrigin.position : transform.position;
                service = new GridPlacementService(boardDefinition, origin);
                IsInitialized = true;

                if (initialTower != null)
                {
                    SelectTower(initialTower);
                }
            }
            catch
            {
                Shutdown();
                throw;
            }
        }

        public void Shutdown()
        {
            if (!IsInitialized)
            {
                return;
            }

            IsInitialized = false;
            CancelActivePointer();
            selectedTower = null;
            selectedCombatDefinition = null;
            hasCandidate = false;
            candidateIsValid = false;
            service = null;
            view?.SetTower(null);
            view?.Hide();
        }

        private void Update()
        {
            if (!IsInitialized)
            {
                return;
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CancelPlacement();
                return;
            }

            if (pointerKind == PointerKind.UiDrag)
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

        public void SelectTower(TowerDefinition definition)
        {
            if (!IsInitialized)
            {
                return;
            }

            selectedCombatDefinition = null;
            ApplyTowerSelection(definition);
        }

        public void SelectTower(TowerCombatDefinition definition)
        {
            if (!IsInitialized)
            {
                return;
            }

            TowerDefinition placementDefinition = definition?.Core?.PlacementDefinition;
            if (definition != null && placementDefinition == null)
            {
                throw new InvalidOperationException($"{definition.name} requires a placement definition.");
            }

            selectedCombatDefinition = definition;
            ApplyTowerSelection(placementDefinition);
        }

        private void ApplyTowerSelection(TowerDefinition definition)
        {
            selectedTower = definition;
            view?.SetTower(definition);

            if (definition == null)
            {
                ClearCandidate();
                return;
            }

            if (hasCandidate)
            {
                RefreshCandidate(candidateCell);
            }
        }

        public void CancelPlacement()
        {
            if (!IsInitialized)
            {
                return;
            }

            CancelActivePointer();
            selectedTower = null;
            selectedCombatDefinition = null;
            ClearCandidate();
            view?.SetTower(null);
        }

        public bool BeginPlacementDrag(
            TowerCombatDefinition definition,
            int pointerId)
        {
            if (!IsInitialized || definition == null)
            {
                return false;
            }

            CancelPlacement();
            SelectTower(definition);
            if (selectedTower == null)
            {
                return false;
            }

            pointerState = PointerState.Tracking;
            pointerKind = PointerKind.UiDrag;
            trackedPointerId = pointerId;
            ClearCandidate();
            return true;
        }

        public void UpdatePlacementDrag(int pointerId, Vector2 screenPosition, bool pointerOverUi)
        {
            if (!MatchesUiDrag(pointerId))
            {
                return;
            }

            if (pointerOverUi || !TryUpdateCandidate(screenPosition))
            {
                ClearCandidate();
            }
        }

        public bool EndPlacementDrag(int pointerId, Vector2 screenPosition, bool pointerOverUi)
        {
            if (!MatchesUiDrag(pointerId))
            {
                return false;
            }

            bool placed = false;
            if (!pointerOverUi && TryUpdateCandidate(screenPosition) && hasCandidate && candidateIsValid)
            {
                placed = TryPlaceCandidate();
            }

            CancelPlacement();
            return placed;
        }

        public bool CancelPlacementDrag(int pointerId)
        {
            if (!MatchesUiDrag(pointerId))
            {
                return false;
            }

            CancelPlacement();
            return true;
        }

        private bool HandlePrimaryTouch()
        {
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen == null)
            {
                return false;
            }

            var touch = touchscreen.primaryTouch;
            bool isTouchInteraction = touch.press.isPressed
                || touch.press.wasPressedThisFrame
                || touch.press.wasReleasedThisFrame
                || pointerKind == PointerKind.Touch;

            if (!isTouchInteraction)
            {
                return false;
            }

            Vector2 position = touch.position.ReadValue();
            int pointerId = touch.touchId.ReadValue();

            if (touch.press.wasPressedThisFrame && pointerState == PointerState.Idle)
            {
                BeginPointer(PointerKind.Touch, pointerId, position);
            }

            if (pointerKind == PointerKind.Touch && pointerState == PointerState.Tracking && touch.press.isPressed)
            {
                TryUpdateCandidate(position);
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
            if (mouse.leftButton.wasPressedThisFrame && pointerState == PointerState.Idle)
            {
                BeginPointer(PointerKind.Mouse, -1, position);
            }

            if (pointerKind == PointerKind.Mouse && pointerState == PointerState.Tracking && mouse.leftButton.isPressed)
            {
                TryUpdateCandidate(position);
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
            trackedPointerId = pointerId;

            if (selectedTower == null || PointerStartedOverUi(kind, pointerId))
            {
                pointerState = PointerState.IgnoredUntilRelease;
                return;
            }

            pointerState = PointerState.Tracking;
            TryUpdateCandidate(screenPosition);
        }

        private void EndPointer(Vector2 screenPosition)
        {
            if (pointerState == PointerState.Tracking)
            {
                bool releaseMappedToBoard = TryUpdateCandidate(screenPosition);
                if (releaseMappedToBoard && hasCandidate && candidateIsValid)
                {
                    TryPlaceCandidate();
                }
            }

            pointerState = PointerState.Idle;
            pointerKind = PointerKind.None;
            trackedPointerId = 0;
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

        private bool TryUpdateCandidate(Vector2 screenPosition)
        {
            if (worldCamera == null || selectedTower == null || service == null)
            {
                return false;
            }

            Ray ray = worldCamera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, placementSurfaceMask, QueryTriggerInteraction.Ignore)
                || !service.TryWorldToCell(hit.point, out GridCell cell))
            {
                return false;
            }

            RefreshCandidate(cell);
            return true;
        }

        private void RefreshCandidate(GridCell cell)
        {
            if (selectedTower == null || service == null)
            {
                ClearCandidate();
                return;
            }

            candidateCell = cell;
            hasCandidate = true;
            candidateIsValid = selectedTower.Prefab != null
                && service.Evaluate(cell, selectedTower.Footprint).Succeeded;
            view?.Show(
                cell,
                selectedTower.Footprint,
                service.GetFootprintBottomCenter(cell, selectedTower.Footprint),
                boardDefinition.CellSize,
                boardDefinition.HeightUnit,
                candidateIsValid);
        }

        private bool TryPlaceCandidate()
        {
            if (!service.TryPlace(
                    candidateCell,
                    selectedTower,
                    selectedCombatDefinition,
                    placedObjectsRoot,
                    out TowerPlacementRecord? placement))
            {
                RefreshCandidate(candidateCell);
                return false;
            }

            if (placement.HasValue)
            {
                PublishTowerPlaced(placement.Value);
            }

            // Keep the chosen tower active for rapid repeated mobile placement.
            RefreshCandidate(candidateCell);
            return true;
        }

        private void PublishTowerPlaced(TowerPlacementRecord placement)
        {
            Delegate[] handlers = TowerPlaced?.GetInvocationList();
            if (handlers == null)
            {
                return;
            }

            for (int index = 0; index < handlers.Length; index++)
            {
                try
                {
                    ((Action<TowerPlacementRecord>)handlers[index])(placement);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }

        private void ClearCandidate()
        {
            hasCandidate = false;
            candidateIsValid = false;
            view?.Hide();
        }

        private void CancelActivePointer()
        {
            pointerState = PointerState.Idle;
            pointerKind = PointerKind.None;
            trackedPointerId = 0;
        }

        private bool MatchesUiDrag(int pointerId)
        {
            return IsInitialized
                && pointerState == PointerState.Tracking
                && pointerKind == PointerKind.UiDrag
                && trackedPointerId == pointerId;
        }

        private void CancelPointerForInterruption()
        {
            if (pointerKind == PointerKind.UiDrag)
            {
                CancelPlacement();
                return;
            }

            CancelActivePointer();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (IsInitialized && pauseStatus)
            {
                CancelPointerForInterruption();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (IsInitialized && !hasFocus)
            {
                CancelPointerForInterruption();
            }
        }
    }
}
