using System;
using TowerDefense3D.Towers;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Coordinates gameplay HUD input and presentation through the level-scoped tower network adapter.
    /// </summary>
    internal sealed class TowerNetworkHudPresenter
    {
        private readonly TowerNetworkSceneAdapter towerNetworkAdapter;
        private readonly TowerNetworkHudView towerNetworkHud;
        private readonly Action requestReturnToMenu;

        public TowerNetworkHudPresenter(
            TowerNetworkSceneAdapter towerNetworkAdapter,
            TowerNetworkHudView towerNetworkHud,
            Action requestReturnToMenu)
        {
            this.towerNetworkAdapter = towerNetworkAdapter;
            this.towerNetworkHud = towerNetworkHud;
            this.requestReturnToMenu = requestReturnToMenu;
        }

        public void Connect()
        {
            towerNetworkAdapter.StateChanged += Refresh;
            towerNetworkHud.TowerDragBegan += HandleTowerDragBegan;
            towerNetworkHud.TowerDragMoved += HandleTowerDragMoved;
            towerNetworkHud.TowerDragEnded += HandleTowerDragEnded;
            towerNetworkHud.TowerDragCanceled += HandleTowerDragCanceled;
            towerNetworkHud.UnlinkRequested += HandleUnlinkRequested;
            towerNetworkHud.StartWaveRequested += HandleStartWaveRequested;
            towerNetworkHud.CancelPlacementRequested += HandleCancelPlacement;
            towerNetworkHud.ReturnToMenuRequested += HandleReturnToMenu;
        }

        public void Shutdown()
        {
            towerNetworkAdapter.StateChanged -= Refresh;
            towerNetworkHud.TowerDragBegan -= HandleTowerDragBegan;
            towerNetworkHud.TowerDragMoved -= HandleTowerDragMoved;
            towerNetworkHud.TowerDragEnded -= HandleTowerDragEnded;
            towerNetworkHud.TowerDragCanceled -= HandleTowerDragCanceled;
            towerNetworkHud.UnlinkRequested -= HandleUnlinkRequested;
            towerNetworkHud.StartWaveRequested -= HandleStartWaveRequested;
            towerNetworkHud.CancelPlacementRequested -= HandleCancelPlacement;
            towerNetworkHud.ReturnToMenuRequested -= HandleReturnToMenu;
        }

        public void Tick()
        {
            if (towerNetworkAdapter.IsRunning)
            {
                Refresh();
            }
        }

        public void Refresh()
        {
            TowerRuntimeView selectedTower = towerNetworkAdapter.SelectedTower;
            string selectedText = selectedTower == null
                ? "Selected: None"
                : $"Selected: {selectedTower.CombatDefinition.Core.DisplayName} ({selectedTower.CombatDefinition.NetworkRole})";
            string chainText = $"Valid chains: {towerNetworkAdapter.ValidChainCount}"
                + $"   Towers: {towerNetworkAdapter.RegisteredTowerCount}";
            string queueText = CreateQueueText(towerNetworkAdapter, selectedTower);
            string feedbackText = string.IsNullOrWhiteSpace(towerNetworkAdapter.LastFeedback)
                ? "Place towers, then drag one tower to another."
                : towerNetworkAdapter.LastFeedback;
            bool simulationRunning = towerNetworkAdapter.IsRunning;

            towerNetworkHud.Render(new TowerNetworkHudState(
                selectedText,
                chainText,
                queueText,
                feedbackText,
                !simulationRunning,
                selectedTower != null && towerNetworkAdapter.CanEditTopology,
                towerNetworkAdapter.HasValidChain && !simulationRunning,
                simulationRunning ? "RUNNING" : "START WAVE",
                !simulationRunning));
        }

        private void HandleTowerDragBegan(
            TowerCombatDefinition definition,
            TowerPlacementPointerEvent pointerEvent)
        {
            towerNetworkAdapter.BeginTowerPlacementDrag(
                definition,
                pointerEvent.PointerId);
        }

        private void HandleTowerDragMoved(TowerPlacementPointerEvent pointerEvent)
        {
            towerNetworkAdapter.UpdateTowerPlacementDrag(
                pointerEvent.PointerId,
                pointerEvent.ScreenPosition,
                pointerEvent.IsOverUi);
        }

        private void HandleTowerDragEnded(TowerPlacementPointerEvent pointerEvent)
        {
            towerNetworkAdapter.EndTowerPlacementDrag(
                pointerEvent.PointerId,
                pointerEvent.ScreenPosition,
                pointerEvent.IsOverUi);
        }

        private void HandleTowerDragCanceled(int pointerId)
        {
            towerNetworkAdapter.CancelTowerPlacementDrag(pointerId);
        }

        private void HandleCancelPlacement()
        {
            towerNetworkAdapter.CancelPlacement();
            Refresh();
        }

        private void HandleUnlinkRequested()
        {
            towerNetworkAdapter.TryUnlinkSelected(out _);
        }

        private void HandleStartWaveRequested()
        {
            towerNetworkAdapter.TryStartSimulation(out _);
        }

        private void HandleReturnToMenu()
        {
            towerNetworkAdapter.CancelPlacement();
            requestReturnToMenu();
        }

        private static string CreateQueueText(
            TowerNetworkSceneAdapter adapter,
            TowerRuntimeView selectedTower)
        {
            if (selectedTower == null)
            {
                return "Queue: select a tower";
            }

            if (!adapter.TryCreateSelectedQueueSummary(out TowerQueueSummary queue))
            {
                return "Queue: unavailable";
            }

            if (queue.Capacity == 0)
            {
                return "Queue: source tower has no input queue";
            }

            return $"Queue: {queue.QueuedProjectileCount} queued + {queue.ReservedProjectileCount} reserved"
                + $" / {queue.Capacity}";
        }
    }
}
