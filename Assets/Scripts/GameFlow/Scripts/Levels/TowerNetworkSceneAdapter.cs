using System;
using System.Collections.Generic;
using TowerDefense3D.GridPlacement;
using TowerDefense3D.Towers;
using UnityEngine;

namespace TowerDefense3D.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class TowerNetworkSceneAdapter : MonoBehaviour, ILevelSceneParticipant, ITowerNetworkSceneRegistry
    {
        [SerializeField] private GridPlacementController placementController;
        [SerializeField] private TowerSimulationDriver simulationDriver;
        [SerializeField] private TowerNetworkInputController inputController;
        [SerializeField] private TowerLinkPresenter linkPresenter;
        [SerializeField] private TowerProjectilePresenter projectilePresenter;

        private readonly Dictionary<TowerNodeId, TowerRuntimeView> viewsByNode =
            new Dictionary<TowerNodeId, TowerRuntimeView>();
        private readonly Dictionary<TowerRuntimeView, TowerNodeId> nodesByView =
            new Dictionary<TowerRuntimeView, TowerNodeId>();

        private TowerNetworkManager manager;

        public event Action StateChanged;

        public bool IsInitialized => manager != null;
        public TowerCatalog Catalog => manager?.Catalog;
        public TowerRuntimeView SelectedTower => inputController != null ? inputController.SelectedTower : null;
        public string LastFeedback => inputController != null ? inputController.LastFeedback : string.Empty;
        public bool HasValidChain => manager != null && manager.HasValidChain;
        public int ValidChainCount => manager?.ValidChainCount ?? 0;
        public bool IsRunning => manager != null && manager.IsRunning;
        public bool CanEditTopology => manager != null && manager.HasLevelSession && !manager.IsRunning;
        public int RegisteredTowerCount => viewsByNode.Count;

        public void Initialize(LevelSceneRuntimeContext context)
        {
            if (!context.IsValid)
            {
                throw new ArgumentException("Tower network received an invalid level runtime context.", nameof(context));
            }

            if (context.TowerNetworkManager == null)
            {
                throw new InvalidOperationException(
                    "Tower network requires a TowerNetworkManager in the level runtime context.");
            }

            if (IsInitialized)
            {
                throw new InvalidOperationException("TowerNetworkSceneAdapter is already initialized.");
            }

            ResolveSceneComponents();
            TowerNetworkManager runtimeManager = context.TowerNetworkManager;
            runtimeManager.BeginLevelSession(context.LevelNumber);
            manager = runtimeManager;

            try
            {
                placementController.TowerPlaced += HandleTowerPlaced;
                runtimeManager.StateChanged += HandleManagerStateChanged;
                inputController.SelectionChanged += HandleInputStateChanged;
                inputController.FeedbackChanged += HandleInputStateChanged;
                inputController.Initialize(this, placementController.WorldCamera, placementController.CancelPlacement);
                linkPresenter.Initialize(runtimeManager, this, inputController);
                projectilePresenter.Initialize(runtimeManager);
                simulationDriver.Initialize(runtimeManager);
                PublishStateChanged();
            }
            catch
            {
                Shutdown();
                throw;
            }
        }

        public void Shutdown()
        {
            if (manager == null)
            {
                return;
            }

            TowerNetworkManager initializedManager = manager;
            placementController.TowerPlaced -= HandleTowerPlaced;
            initializedManager.StateChanged -= HandleManagerStateChanged;

            if (inputController != null)
            {
                inputController.SelectionChanged -= HandleInputStateChanged;
                inputController.FeedbackChanged -= HandleInputStateChanged;
                inputController.Shutdown();
            }

            linkPresenter?.Shutdown();
            projectilePresenter?.Shutdown();
            simulationDriver?.Shutdown();
            ClearRegisteredViews();
            manager = null;
            initializedManager.EndLevelSession();
            PublishStateChanged();
        }

        public IReadOnlyList<TowerRuntimeView> CreateTowerViewSnapshot()
        {
            if (manager == null)
            {
                return Array.Empty<TowerRuntimeView>();
            }

            IReadOnlyList<TowerNodeId> orderedNodeIds = manager.CreateNodeIdSnapshot();
            var snapshot = new List<TowerRuntimeView>(orderedNodeIds.Count);
            for (int index = 0; index < orderedNodeIds.Count; index++)
            {
                if (viewsByNode.TryGetValue(orderedNodeIds[index], out TowerRuntimeView view) && view != null)
                {
                    snapshot.Add(view);
                }
            }

            return snapshot;
        }

        public bool TryGetTowerView(TowerNodeId nodeId, out TowerRuntimeView view)
        {
            return viewsByNode.TryGetValue(nodeId, out view) && view != null;
        }

        public bool TryRewire(TowerRuntimeView source, TowerRuntimeView target, out string error)
        {
            if (manager == null)
            {
                error = "Tower network is not initialized.";
                return false;
            }

            if (source == null || target == null
                || !nodesByView.TryGetValue(source, out TowerNodeId sourceId)
                || !nodesByView.TryGetValue(target, out TowerNodeId targetId))
            {
                error = "Both link endpoints must be registered towers.";
                return false;
            }

            return manager.TryRewire(sourceId, targetId, out error);
        }

        public void SelectTowerForPlacement(TowerCombatDefinition definition)
        {
            if (!IsInitialized)
            {
                return;
            }

            inputController.ClearSelection();
            inputController.ReportFeedback(definition == null
                ? string.Empty
                : $"Placing {definition.Core.DisplayName}.");
            placementController.SelectTower(definition);
        }

        public void CancelPlacement()
        {
            placementController?.CancelPlacement();
        }

        public void ClearSelection()
        {
            inputController?.ClearSelection();
        }

        public bool TryUnlinkSelected(out string error)
        {
            TowerRuntimeView selectedTower = SelectedTower;
            if (manager == null || selectedTower == null || !nodesByView.TryGetValue(selectedTower, out TowerNodeId nodeId))
            {
                error = "Select a registered tower before unlinking.";
                inputController?.ReportFeedback(error);
                return false;
            }

            bool succeeded = manager.TryUnlinkAll(nodeId, out error);
            inputController.ReportFeedback(succeeded ? $"Unlinked {GetDisplayName(selectedTower)}." : error);
            return succeeded;
        }

        public bool TryStartSimulation(out string error)
        {
            if (manager == null)
            {
                error = "Tower network is not initialized.";
                return false;
            }

            placementController.CancelPlacement();
            inputController.ClearSelection();
            bool succeeded = manager.TryStartSimulation(out error);
            inputController.ReportFeedback(succeeded ? "Tower simulation started." : error);
            return succeeded;
        }

        public bool TryCreateSelectedQueueSummary(out TowerQueueSummary summary)
        {
            TowerRuntimeView selectedTower = SelectedTower;
            if (manager == null || selectedTower == null || !selectedTower.NodeId.IsValid
                || !manager.TryGetNodeSpec(selectedTower.NodeId, out TowerRuntimeSpec spec))
            {
                summary = default;
                return false;
            }

            int queued = 0;
            int reserved = 0;
            int capacity = 0;
            for (int inputPort = 0; inputPort < spec.InputPortCount; inputPort++)
            {
                if (manager.TryCreateInputPortSnapshot(
                    selectedTower.NodeId,
                    inputPort,
                    out TowerInputPortSnapshot port))
                {
                    queued += port.QueuedProjectileCount;
                    reserved += port.ReservedProjectileCount;
                    capacity += port.Capacity;
                }
            }

            summary = new TowerQueueSummary(queued, reserved, capacity);
            return true;
        }

        private void ResolveSceneComponents()
        {
            placementController = placementController != null
                ? placementController
                : GetComponent<GridPlacementController>();
            simulationDriver = simulationDriver != null
                ? simulationDriver
                : GetComponent<TowerSimulationDriver>();
            inputController = inputController != null
                ? inputController
                : GetComponent<TowerNetworkInputController>();
            linkPresenter = linkPresenter != null
                ? linkPresenter
                : GetComponent<TowerLinkPresenter>();
            projectilePresenter = projectilePresenter != null
                ? projectilePresenter
                : GetComponent<TowerProjectilePresenter>();

            if (placementController == null || simulationDriver == null || inputController == null
                || linkPresenter == null || projectilePresenter == null)
            {
                throw new InvalidOperationException(
                    "TowerNetworkSceneAdapter requires GridPlacementController, TowerSimulationDriver, "
                    + "TowerNetworkInputController, TowerLinkPresenter, and TowerProjectilePresenter on the same object.");
            }
        }

        private void HandleTowerPlaced(TowerPlacementRecord placement)
        {
            if (manager == null || placement.RuntimeView == null || nodesByView.ContainsKey(placement.RuntimeView))
            {
                return;
            }

            Vector3 position = placement.RuntimeView.transform.position;
            TowerNodeId nodeId = manager.RegisterTower(
                placement.CombatDefinition,
                new TowerWorldPosition(position.x, position.y, position.z));

            try
            {
                placement.RuntimeView.BindNode(nodeId);
                viewsByNode.Add(nodeId, placement.RuntimeView);
                nodesByView.Add(placement.RuntimeView, nodeId);
                placement.RuntimeView.Destroyed += HandleTowerDestroyed;
                inputController.ReportFeedback($"Placed {GetDisplayName(placement.RuntimeView)}.");
                PublishStateChanged();
            }
            catch
            {
                placement.RuntimeView.ClearNodeBinding();
                manager.UnregisterTower(nodeId);
                throw;
            }
        }

        private void HandleTowerDestroyed(TowerRuntimeView view)
        {
            if (ReferenceEquals(view, null))
            {
                return;
            }

            TowerNodeId nodeId = view.NodeId;
            if (!nodeId.IsValid || !viewsByNode.ContainsKey(nodeId))
            {
                return;
            }

            view.Destroyed -= HandleTowerDestroyed;
            nodesByView.Remove(view);
            viewsByNode.Remove(nodeId);
            inputController.ClearSelection();

            if (manager != null && manager.HasLevelSession)
            {
                manager.StopSimulation();
                manager.UnregisterTower(nodeId);
            }

            PublishStateChanged();
        }

        private void ClearRegisteredViews()
        {
            foreach (TowerRuntimeView view in nodesByView.Keys)
            {
                if (view != null)
                {
                    view.Destroyed -= HandleTowerDestroyed;
                    view.ClearNodeBinding();
                }
            }

            nodesByView.Clear();
            viewsByNode.Clear();
        }

        private void HandleManagerStateChanged()
        {
            PublishStateChanged();
        }

        private void HandleInputStateChanged()
        {
            PublishStateChanged();
        }

        private void PublishStateChanged()
        {
            StateChanged?.Invoke();
        }

        private static string GetDisplayName(TowerRuntimeView view)
        {
            string displayName = view?.CombatDefinition?.Core?.DisplayName;
            return string.IsNullOrWhiteSpace(displayName) ? "Tower" : displayName;
        }

        private void OnDestroy()
        {
            Shutdown();
        }
    }
}
