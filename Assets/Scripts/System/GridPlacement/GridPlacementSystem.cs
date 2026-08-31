using System;
using TowerDefense3D.GameplayInput;
using UnityEngine;

namespace TowerDefense3D.GridPlacement
{
    /// <summary>
    /// Owns level placement selection, pointer state, candidates, and placement transactions.
    /// </summary>
    public sealed class GridPlacementSystem
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
            Gameplay,
            UiDrag
        }

        private readonly GameplayInputSystem inputSystem;
        private readonly IGridPlacementView view;
        private readonly ITowerInstanceFactory instanceFactory;
        private readonly GridPlacementModel model;

        private TowerDefinition selectedTower;
        private PointerState pointerState;
        private PointerKind pointerKind;
        private int trackedPointerId;
        private bool hasCandidate;
        private bool candidateIsValid;
        private GridCell candidateCell;

        public GridPlacementSystem(
            BoardSystem boardSystem,
            GameplayInputSystem inputSystem,
            IGridPlacementView view,
            ITowerInstanceFactory instanceFactory)
        {
            if (boardSystem == null)
            {
                throw new ArgumentNullException(nameof(boardSystem));
            }

            this.inputSystem = inputSystem ?? throw new ArgumentNullException(nameof(inputSystem));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.instanceFactory = instanceFactory ?? throw new ArgumentNullException(nameof(instanceFactory));
            model = new GridPlacementModel(boardSystem.Definition, boardSystem.Board);
        }

        public TowerDefinition SelectedTower => selectedTower;
        public bool IsPlacementActive => selectedTower != null;
        public bool HasCandidate => hasCandidate;
        public bool CandidateIsValid => candidateIsValid;
        public GridCell CandidateCell => candidateCell;
        public GridOccupancy Occupancy => model.Occupancy;

        public event Action<GridPlacementCommit> TowerPlaced;

        public void Tick()
        {
            GameplayInputSnapshot input = inputSystem.Current;
            if (input.WasInterrupted)
            {
                CancelPointerForInterruption();
                return;
            }

            if (input.CancelRequested)
            {
                CancelPlacement();
                return;
            }

            if (inputSystem.Mode != GameplayInputMode.GridPlacement
                || pointerKind == PointerKind.UiDrag
                || !input.HasPointerInput)
            {
                return;
            }

            if (input.WasPressed && pointerState == PointerState.Idle)
            {
                BeginGameplayPointer(input);
            }

            if (pointerState == PointerState.Tracking
                && input.IsPressed
                && input.PointerId == trackedPointerId)
            {
                TryUpdateCandidate(input.ScreenPosition);
            }

            if (pointerKind == PointerKind.Gameplay
                && input.WasReleased
                && input.PointerId == trackedPointerId)
            {
                EndGameplayPointer(input.ScreenPosition);
            }
        }

        public void SelectTower(TowerDefinition definition)
        {
            if (definition == null)
            {
                CancelPlacement();
                return;
            }

            selectedTower = definition;
            inputSystem.SetMode(GameplayInputMode.GridPlacement);
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
            inputSystem.ClearMode(GameplayInputMode.GridPlacement);
        }

        /// <summary>
        /// Claims the cells under a tower the level authored into the scene, so the board treats
        /// it like any built tower and the player cannot drop another one on top of it. The
        /// snapped world position is handed back because an authored transform rarely lands
        /// exactly on the footprint's own bottom center.
        /// </summary>
        public bool TryOccupyAuthoredTower(
            Vector3 worldPosition,
            TowerFootprint footprint,
            out Vector3 snappedPosition,
            out int ownerId)
        {
            snappedPosition = worldPosition;
            ownerId = 0;
            if (!model.TryWorldToCell(worldPosition, out GridCell cell)
                || !model.Evaluate(cell, footprint).Succeeded
                || !model.TryReserve(cell, footprint, out PlacementReservation reservation))
            {
                return false;
            }

            using (reservation)
            {
                ownerId = model.NextOwnerId();
                reservation.Commit(ownerId);
            }

            snappedPosition = model.GetFootprintBottomCenter(cell, footprint);
            return true;
        }

        public bool BeginPlacementDrag(TowerDefinition definition, int pointerId)
        {
            CancelPlacement();
            SelectTower(definition);
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
            if (!pointerOverUi
                && TryUpdateCandidate(screenPosition)
                && candidateIsValid)
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

        private void BeginGameplayPointer(GameplayInputSnapshot input)
        {
            pointerKind = PointerKind.Gameplay;
            trackedPointerId = input.PointerId;
            if (input.IsPointerOverUi)
            {
                pointerState = PointerState.IgnoredUntilRelease;
                return;
            }

            pointerState = PointerState.Tracking;
            TryUpdateCandidate(input.ScreenPosition);
        }

        private void EndGameplayPointer(Vector2 screenPosition)
        {
            if (pointerState == PointerState.Tracking
                && TryUpdateCandidate(screenPosition)
                && candidateIsValid)
            {
                TryPlaceCandidate();
            }

            CancelActivePointer();
        }

        private bool TryUpdateCandidate(Vector2 screenPosition)
        {
            if (!view.TryGetWorldPoint(screenPosition, out Vector3 worldPoint)
                || !model.TryWorldToCell(worldPoint, out GridCell cell))
            {
                return false;
            }

            RefreshCandidate(cell);
            return true;
        }

        private void RefreshCandidate(GridCell cell)
        {
            candidateCell = cell;
            hasCandidate = true;
            candidateIsValid = selectedTower.Prefab != null
                && model.Evaluate(cell, selectedTower.Footprint).Succeeded;
            view.Show(
                selectedTower.Footprint,
                model.GetFootprintBottomCenter(cell, selectedTower.Footprint),
                model.CellSize,
                model.HeightUnit,
                candidateIsValid);
        }

        private bool TryPlaceCandidate()
        {
            model.TryReserve(
                candidateCell,
                selectedTower.Footprint,
                out PlacementReservation reservation);

            GameObject instance = null;
            int ownerId = 0;
            using (reservation)
            {
                Vector3 position = model.GetFootprintBottomCenter(candidateCell, selectedTower.Footprint);
                if (!instanceFactory.TryCreate(selectedTower, position, out instance))
                {
                    reservation.Rollback();
                    RefreshCandidate(candidateCell);
                    return false;
                }

                ownerId = model.NextOwnerId();
                reservation.Commit(ownerId);
            }

            var placement = new GridPlacementCommit(
                selectedTower,
                instance,
                candidateCell,
                ownerId);
            RefreshCandidate(candidateCell);
            PublishTowerPlaced(placement);
            return true;
        }

        private void PublishTowerPlaced(GridPlacementCommit placement)
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
                    ((Action<GridPlacementCommit>)handlers[index])(placement);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        private void ClearCandidate()
        {
            hasCandidate = false;
            candidateIsValid = false;
            view.Hide();
        }

        private void CancelActivePointer()
        {
            pointerState = PointerState.Idle;
            pointerKind = PointerKind.None;
            trackedPointerId = 0;
        }

        private bool MatchesUiDrag(int pointerId)
        {
            return pointerState == PointerState.Tracking
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
    }

    public readonly struct GridPlacementCommit
    {
        public GridPlacementCommit(
            TowerDefinition definition,
            GameObject instance,
            GridCell anchor,
            int ownerId)
        {
            Definition = definition;
            Instance = instance;
            Anchor = anchor;
            OwnerId = ownerId;
        }

        public TowerDefinition Definition { get; }
        public GameObject Instance { get; }
        public GridCell Anchor { get; }
        public int OwnerId { get; }
    }
}
