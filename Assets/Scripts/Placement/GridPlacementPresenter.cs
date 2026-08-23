using System;
using TowerDefense3D.Towers;
using UnityEngine;

namespace TowerDefense3D.GridPlacement
{
    /// <summary>
    /// Temporary compatibility bridge from existing level adapters to GridPlacementSystem.
    /// </summary>
    public sealed class GridPlacementPresenter : MonoBehaviour
    {
        [SerializeField] private TowerDefinition initialTower;
        [SerializeField] private Camera worldCamera;

        private GridPlacementSystem placementSystem;
        private GridPlacementView placementView;
        private TowerCombatDefinition selectedCombatDefinition;

        public TowerDefinition SelectedTower => placementSystem?.SelectedTower;
        public TowerCombatDefinition SelectedCombatDefinition => selectedCombatDefinition;
        public Camera WorldCamera => placementView != null ? placementView.WorldCamera : worldCamera;
        public bool IsPlacementActive => placementSystem?.IsPlacementActive == true;
        public bool HasCandidate => placementSystem?.HasCandidate == true;
        public bool CandidateIsValid => placementSystem?.CandidateIsValid == true;
        public GridCell CandidateCell => placementSystem != null
            ? placementSystem.CandidateCell
            : default;
        public GridOccupancy Occupancy => placementSystem?.Occupancy;
        public bool IsInitialized => placementSystem != null;

        public event Action<TowerPlacementRecord> TowerPlaced;

        public void Bind(GridPlacementSystem system, GridPlacementView view)
        {
            if (placementSystem != null)
            {
                throw new InvalidOperationException("GridPlacementPresenter is already bound.");
            }

            placementSystem = system ?? throw new ArgumentNullException(nameof(system));
            placementView = view ?? throw new ArgumentNullException(nameof(view));
            placementSystem.TowerPlaced += HandleTowerPlaced;

            if (initialTower != null)
            {
                placementSystem.SelectTower(initialTower);
            }
        }

        public void Initialize()
        {
            if (placementSystem == null)
            {
                throw new InvalidOperationException(
                    "LevelLifetimeScope must bind GridPlacementPresenter before adapters initialize.");
            }
        }

        public void Shutdown()
        {
            if (placementSystem == null)
            {
                return;
            }

            placementSystem.TowerPlaced -= HandleTowerPlaced;
            placementSystem.CancelPlacement();
            placementSystem = null;
            placementView = null;
            selectedCombatDefinition = null;
        }

        public void SelectTower(TowerDefinition definition)
        {
            selectedCombatDefinition = null;
            RequireSystem().SelectTower(definition);
        }

        public void SelectTower(TowerCombatDefinition definition)
        {
            TowerDefinition placementDefinition = definition?.Core?.PlacementDefinition;
            if (definition != null && placementDefinition == null)
            {
                throw new InvalidOperationException(definition.name + " requires a placement definition.");
            }

            selectedCombatDefinition = definition;
            RequireSystem().SelectTower(placementDefinition);
        }

        public void CancelPlacement()
        {
            selectedCombatDefinition = null;
            placementSystem?.CancelPlacement();
        }

        public bool BeginPlacementDrag(TowerCombatDefinition definition, int pointerId)
        {
            if (definition == null)
            {
                return false;
            }

            TowerDefinition placementDefinition = definition.Core?.PlacementDefinition;
            if (placementDefinition == null)
            {
                throw new InvalidOperationException(definition.name + " requires a placement definition.");
            }

            selectedCombatDefinition = definition;
            return RequireSystem().BeginPlacementDrag(placementDefinition, pointerId);
        }

        public void UpdatePlacementDrag(int pointerId, Vector2 screenPosition, bool pointerOverUi)
        {
            RequireSystem().UpdatePlacementDrag(pointerId, screenPosition, pointerOverUi);
        }

        public bool EndPlacementDrag(int pointerId, Vector2 screenPosition, bool pointerOverUi)
        {
            bool placed = RequireSystem().EndPlacementDrag(pointerId, screenPosition, pointerOverUi);
            if (!placementSystem.IsPlacementActive)
            {
                selectedCombatDefinition = null;
            }

            return placed;
        }

        public bool CancelPlacementDrag(int pointerId)
        {
            bool cancelled = RequireSystem().CancelPlacementDrag(pointerId);
            if (cancelled)
            {
                selectedCombatDefinition = null;
            }

            return cancelled;
        }

        private GridPlacementSystem RequireSystem()
        {
            return placementSystem
                ?? throw new InvalidOperationException("GridPlacementPresenter is not bound to its level scope.");
        }

        private void HandleTowerPlaced(GridPlacementCommit placement)
        {
            if (selectedCombatDefinition == null)
            {
                return;
            }

            TowerRuntimeView runtimeView = placement.Instance.GetComponent<TowerRuntimeView>();
            if (runtimeView == null)
            {
                runtimeView = placement.Instance.AddComponent<TowerRuntimeView>();
            }

            runtimeView.Configure(selectedCombatDefinition);
            PublishTowerPlaced(
                new TowerPlacementRecord(
                    selectedCombatDefinition,
                    placement.Definition,
                    runtimeView,
                    placement.Anchor,
                    placement.OwnerId));
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
    }
}
