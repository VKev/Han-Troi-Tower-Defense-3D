using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TowerDefense3D.GridPlacement
{
    /// <summary>
    /// Scene-scoped owner for one mobile placement interaction.
    /// </summary>
    public sealed class GridPlacementController : MonoBehaviour
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
            Mouse
        }

        [Header("Board")]
        [SerializeField] private BoardDefinition boardDefinition;
        [SerializeField] private Transform boardOrigin;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private LayerMask placementSurfaceMask = ~0;
        [SerializeField, Min(1f)] private float maxRayDistance = 500f;

        [Header("Placement")]
        [SerializeField] private GridPlacementPreview preview;
        [SerializeField] private Transform placedObjectsRoot;
        [SerializeField] private TowerDefinition initialTower;

        private GridBoard board;
        private GridOccupancy occupancy;
        private PlacementValidator validator;
        private TowerDefinition selectedTower;
        private PointerState pointerState;
        private PointerKind pointerKind;
        private int trackedPointerId;
        private bool hasCandidate;
        private bool candidateIsValid;
        private GridCell candidateCell;
        private int nextOwnerId = 1;

        public TowerDefinition SelectedTower => selectedTower;
        public bool HasCandidate => hasCandidate;
        public bool CandidateIsValid => hasCandidate && candidateIsValid;
        public GridCell CandidateCell => candidateCell;
        public GridOccupancy Occupancy => occupancy;

        private void Awake()
        {
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            if (boardDefinition == null)
            {
                Debug.LogError("GridPlacementController requires a BoardDefinition.", this);
                enabled = false;
                return;
            }

            Vector3 origin = boardOrigin != null ? boardOrigin.position : transform.position;
            board = new GridBoard(boardDefinition, origin);
            occupancy = new GridOccupancy(boardDefinition.Dimensions);
            validator = new PlacementValidator(board, occupancy);
        }

        private void Start()
        {
            if (initialTower != null)
            {
                SelectTower(initialTower);
            }
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CancelPlacement();
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
            selectedTower = definition;
            preview?.SetTower(definition);

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
            CancelActivePointer();
            selectedTower = null;
            ClearCandidate();
            preview?.SetTower(null);
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
            if (worldCamera == null || selectedTower == null || board == null)
            {
                return false;
            }

            Ray ray = worldCamera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, placementSurfaceMask, QueryTriggerInteraction.Ignore)
                || !board.Mapper.TryWorldToCell(hit.point, out GridCell cell))
            {
                return false;
            }

            RefreshCandidate(cell);
            return true;
        }

        private void RefreshCandidate(GridCell cell)
        {
            if (selectedTower == null || validator == null)
            {
                ClearCandidate();
                return;
            }

            candidateCell = cell;
            hasCandidate = true;
            candidateIsValid = selectedTower.Prefab != null
                && validator.Evaluate(cell, selectedTower.Footprint).Succeeded;
            preview?.Show(
                cell,
                selectedTower.Footprint,
                GetFootprintBottomCenter(cell, selectedTower.Footprint),
                boardDefinition.CellSize,
                boardDefinition.HeightUnit,
                candidateIsValid);
        }

        private void TryPlaceCandidate()
        {
            TowerDefinition tower = selectedTower;
            if (tower == null || tower.Prefab == null)
            {
                candidateIsValid = false;
                RefreshCandidate(candidateCell);
                return;
            }

            PlacementResult currentResult = validator.Evaluate(candidateCell, tower.Footprint);
            if (!currentResult.Succeeded
                || !occupancy.TryReserve(candidateCell, tower.Footprint, out PlacementReservation reservation))
            {
                RefreshCandidate(candidateCell);
                return;
            }

            GameObject instance = null;
            using (reservation)
            {
                try
                {
                    instance = Instantiate(
                        tower.Prefab,
                        GetFootprintBottomCenter(candidateCell, tower.Footprint),
                        Quaternion.identity,
                        placedObjectsRoot);

                    int ownerId = NextOwnerId();
                    if (instance == null || !reservation.Commit(ownerId))
                    {
                        if (instance != null)
                        {
                            Destroy(instance);
                        }

                        RefreshCandidate(candidateCell);
                        return;
                    }
                }
                catch (Exception exception)
                {
                    if (instance != null)
                    {
                        Destroy(instance);
                    }

                    Debug.LogException(exception, this);
                    RefreshCandidate(candidateCell);
                    return;
                }
            }

            // Keep the chosen tower active for rapid repeated mobile placement.
            RefreshCandidate(candidateCell);
        }

        private Vector3 GetFootprintBottomCenter(GridCell anchor, TowerFootprint footprint)
        {
            Vector3 center = board.Mapper.CellToWorldCenter(anchor);
            if ((footprint.Width & 1) == 0)
            {
                center.x += boardDefinition.CellSize * 0.5f;
            }

            if ((footprint.Depth & 1) == 0)
            {
                center.z += boardDefinition.CellSize * 0.5f;
            }

            return center;
        }

        private int NextOwnerId()
        {
            if (nextOwnerId == int.MaxValue)
            {
                nextOwnerId = 1;
            }

            return nextOwnerId++;
        }

        private void ClearCandidate()
        {
            hasCandidate = false;
            candidateIsValid = false;
            preview?.Hide();
        }

        private void CancelActivePointer()
        {
            pointerState = PointerState.Idle;
            pointerKind = PointerKind.None;
            trackedPointerId = 0;
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                CancelActivePointer();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                CancelActivePointer();
            }
        }
    }
}
