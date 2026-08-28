using System;
using System.Collections.Generic;
using TowerDefense3D.Economy;
using TowerDefense3D.GridPlacement;
using UnityEngine;

namespace TowerDefense3D.Towers
{
    /// <summary>
    /// Level-scoped facade for tower registration, topology, simulation commands, and HUD state.
    /// </summary>
    public sealed class TowerNetworkSystem : IDisposable
    {
        private readonly TowerNetworkManager manager;
        private readonly GridPlacementSystem placementSystem;
        private readonly LevelGoldSystem goldSystem;
        private readonly TowerRuntimeViewRegistry viewRegistry;
        private readonly int levelNumber;

        private TowerCombatDefinition placementCombatDefinition;
        private ITowerRuntimeView selectedTower;
        private string lastFeedback = string.Empty;
        private bool isStarted;

        public TowerNetworkSystem(
            TowerNetworkManager manager,
            GridPlacementSystem placementSystem,
            int levelNumber,
            LevelGoldSystem goldSystem)
        {
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
            this.placementSystem = placementSystem ?? throw new ArgumentNullException(nameof(placementSystem));
            this.goldSystem = goldSystem ?? throw new ArgumentNullException(nameof(goldSystem));
            if (levelNumber <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(levelNumber), "Level number must be positive.");
            }

            this.levelNumber = levelNumber;
            viewRegistry = new TowerRuntimeViewRegistry(HandleTowerDestroyed);
        }

        public event Action StateChanged;

        public TowerNetworkManager Manager => manager;
        public ITowerRuntimeView SelectedTower => selectedTower;
        public string LastFeedback => lastFeedback;
        public bool HasValidChain => manager.HasValidChain;
        public int ValidChainCount => manager.ValidChainCount;
        public bool IsRunning => manager.IsRunning;
        public bool CanEditTopology => manager.HasLevelSession && !manager.IsRunning;
        public int RegisteredTowerCount => viewRegistry.Count;

        public void Start()
        {
            manager.BeginLevelSession(levelNumber);
            manager.StateChanged += HandleManagerStateChanged;
            placementSystem.TowerPlaced += HandleTowerPlaced;
            isStarted = true;
            PublishStateChanged();
        }

        public void Dispose()
        {
            if (!isStarted)
            {
                return;
            }

            isStarted = false;
            placementSystem.TowerPlaced -= HandleTowerPlaced;
            manager.StateChanged -= HandleManagerStateChanged;
            placementSystem.CancelPlacement();
            placementCombatDefinition = null;
            selectedTower = null;
            lastFeedback = string.Empty;
            viewRegistry.Clear();
            manager.EndLevelSession();
        }

        public IReadOnlyList<ITowerRuntimeView> CreateTowerViewSnapshot()
        {
            return viewRegistry.CreateSnapshot(manager.CreateNodeIdSnapshot());
        }

        public bool TryGetTowerView(TowerNodeId nodeId, out ITowerRuntimeView view)
        {
            return viewRegistry.TryGetView(nodeId, out view);
        }

        public bool TryRewire(ITowerRuntimeView source, ITowerRuntimeView target, out string error)
        {
            if (source == null || target == null
                || !viewRegistry.TryGetNodeId(source, out TowerNodeId sourceId)
                || !viewRegistry.TryGetNodeId(target, out TowerNodeId targetId))
            {
                error = "Both link endpoints must be registered towers.";
                return false;
            }

            return manager.TryRewire(sourceId, targetId, out error);
        }

        public bool BeginTowerPlacementDrag(TowerCombatDefinition definition, int pointerId)
        {
            if (!CanEditTopology)
            {
                ReportFeedback("Tower placement is locked while simulation is running.");
                return false;
            }

            if (definition == null)
            {
                return false;
            }

            if (!goldSystem.CanAfford(GetBuildCost(definition)))
            {
                ReportFeedback("Not enough Gold.");
                return false;
            }

            TowerDefinition placementDefinition = definition.Core.PlacementDefinition;
            if (placementDefinition == null)
            {
                throw new InvalidOperationException(definition.name + " requires a placement definition.");
            }

            ClearSelection();
            placementCombatDefinition = definition;
            placementSystem.BeginPlacementDrag(placementDefinition, pointerId);
            ReportFeedback($"Drag {definition.Core.DisplayName} onto the board.");
            return true;
        }

        public void UpdateTowerPlacementDrag(int pointerId, Vector2 screenPosition, bool pointerOverUi)
        {
            if (CanEditTopology)
            {
                placementSystem.UpdatePlacementDrag(pointerId, screenPosition, pointerOverUi);
            }
        }

        public bool EndTowerPlacementDrag(int pointerId, Vector2 screenPosition, bool pointerOverUi)
        {
            if (!CanEditTopology)
            {
                placementSystem.CancelPlacementDrag(pointerId);
                placementCombatDefinition = null;
                return false;
            }

            TowerCombatDefinition definition = placementCombatDefinition;
            if (definition == null || !goldSystem.TrySpend(GetBuildCost(definition)))
            {
                placementSystem.CancelPlacementDrag(pointerId);
                placementCombatDefinition = null;
                ReportFeedback("Not enough Gold.");
                return false;
            }

            bool placed = placementSystem.EndPlacementDrag(pointerId, screenPosition, pointerOverUi);
            placementCombatDefinition = null;
            if (!placed)
            {
                goldSystem.Add(GetBuildCost(definition));
                ReportFeedback("Tower placement canceled.");
            }

            return placed;
        }

        public void CancelTowerPlacementDrag(int pointerId)
        {
            if (placementSystem.CancelPlacementDrag(pointerId))
            {
                placementCombatDefinition = null;
                ReportFeedback("Tower placement canceled.");
            }
        }

        public void CancelPlacement()
        {
            placementCombatDefinition = null;
            placementSystem.CancelPlacement();
        }

        public void Select(ITowerRuntimeView tower)
        {
            ITowerRuntimeView nextSelection = tower != null && tower.IsRegistered ? tower : null;
            if (ReferenceEquals(selectedTower, nextSelection))
            {
                return;
            }

            selectedTower = nextSelection;
            PublishStateChanged();
        }

        public void ClearSelection()
        {
            Select(null);
        }

        public void ReportFeedback(string message)
        {
            string normalized = message ?? string.Empty;
            if (string.Equals(lastFeedback, normalized, StringComparison.Ordinal))
            {
                return;
            }

            lastFeedback = normalized;
            PublishStateChanged();
        }

        public bool TryUnlinkSelected(out string error)
        {
            if (selectedTower == null)
            {
                error = "Select a registered tower before unlinking.";
                ReportFeedback(error);
                return false;
            }

            TowerNodeId nodeId = viewRegistry.GetNodeId(selectedTower);
            bool succeeded = manager.TryUnlinkAll(nodeId, out error);
            ReportFeedback(succeeded ? $"Unlinked {GetDisplayName(selectedTower)}." : error);
            return succeeded;
        }

        public bool TryStartSimulation(out string error)
        {
            CancelPlacement();
            ClearSelection();
            bool succeeded = manager.TryStartSimulation(out error);
            ReportFeedback(succeeded ? "Tower simulation started." : error);
            return succeeded;
        }

        public void StopSimulation()
        {
            manager.StopSimulation();
            ReportFeedback("Tower simulation stopped.");
        }

        public bool TryCreateSelectedQueueSummary(out TowerQueueSummary summary)
        {
            if (selectedTower == null)
            {
                summary = default;
                return false;
            }

            return manager.TryCreateQueueSummary(selectedTower.NodeId, out summary);
        }

        private void HandleTowerPlaced(GridPlacementCommit placement)
        {
            TowerCombatDefinition combatDefinition = placementCombatDefinition;
            if (combatDefinition == null)
            {
                return;
            }

            var runtimeView = placement.Instance.GetComponent(typeof(ITowerRuntimeView)) as ITowerRuntimeView;
            if (runtimeView == null)
            {
                throw new InvalidOperationException(
                    $"Tower prefab '{placement.Instance.name}' must author a TowerRuntimeView component.");
            }

            runtimeView.Configure(combatDefinition);
            Vector3 position = runtimeView.ProjectileOrigin;
            TowerNodeId nodeId = manager.RegisterTower(
                combatDefinition,
                new TowerWorldPosition(position.x, position.y, position.z));

            try
            {
                viewRegistry.Register(nodeId, runtimeView);
                ReportFeedback($"Placed {GetDisplayName(runtimeView)}.");
            }
            catch
            {
                manager.UnregisterTower(nodeId);
                throw;
            }
        }

        private void HandleTowerDestroyed(TowerNodeId nodeId)
        {
            ClearSelection();
            manager.StopSimulation();
            manager.UnregisterTower(nodeId);
            PublishStateChanged();
        }

        private void HandleManagerStateChanged()
        {
            PublishStateChanged();
        }

        private void PublishStateChanged()
        {
            StateChanged?.Invoke();
        }

        private static string GetDisplayName(ITowerRuntimeView view)
        {
            return view.CombatDefinition.Core.DisplayName;
        }

        private static int GetBuildCost(TowerCombatDefinition definition)
        {
            TowerEconomyProfile economy = definition.Core.Economy;
            if (economy == null)
            {
                throw new InvalidOperationException(definition.name + " requires an economy profile.");
            }

            return economy.BuildCost;
        }
    }
}
