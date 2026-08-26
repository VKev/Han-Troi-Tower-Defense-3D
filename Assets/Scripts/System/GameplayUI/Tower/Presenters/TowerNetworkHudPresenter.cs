using System;
using TowerDefense3D.Towers;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Converts tower-network state and HUD commands between the model and its view.
    /// </summary>
    public sealed class TowerNetworkHudPresenter
    {
        private readonly TowerNetworkSystem towerNetworkSystem;
        private readonly ITowerNetworkHudView towerNetworkHud;
        private Action requestReturnToMenu;

        public TowerNetworkHudPresenter(
            TowerNetworkSystem towerNetworkSystem,
            ITowerNetworkHudView towerNetworkHud)
        {
            this.towerNetworkSystem = towerNetworkSystem
                ?? throw new ArgumentNullException(nameof(towerNetworkSystem));
            this.towerNetworkHud = towerNetworkHud
                ?? throw new ArgumentNullException(nameof(towerNetworkHud));
        }

        public void BindReturnToMenu(Action request)
        {
            requestReturnToMenu = request ?? throw new ArgumentNullException(nameof(request));
        }

        public void Connect()
        {
            towerNetworkHud.Initialize();
            towerNetworkHud.TowerDragBegan += HandleTowerDragBegan;
            towerNetworkHud.TowerDragMoved += HandleTowerDragMoved;
            towerNetworkHud.TowerDragEnded += HandleTowerDragEnded;
            towerNetworkHud.TowerDragCanceled += HandleTowerDragCanceled;
            towerNetworkHud.UnlinkRequested += HandleUnlinkRequested;
            towerNetworkHud.CancelPlacementRequested += HandleCancelPlacement;
            towerNetworkHud.ReturnToMenuRequested += HandleReturnToMenu;
            towerNetworkHud.Show();
        }

        public void Disconnect()
        {
            towerNetworkHud.TowerDragBegan -= HandleTowerDragBegan;
            towerNetworkHud.TowerDragMoved -= HandleTowerDragMoved;
            towerNetworkHud.TowerDragEnded -= HandleTowerDragEnded;
            towerNetworkHud.TowerDragCanceled -= HandleTowerDragCanceled;
            towerNetworkHud.UnlinkRequested -= HandleUnlinkRequested;
            towerNetworkHud.CancelPlacementRequested -= HandleCancelPlacement;
            towerNetworkHud.ReturnToMenuRequested -= HandleReturnToMenu;
        }

        public void Refresh()
        {
            ITowerRuntimeView selectedTower = towerNetworkSystem.SelectedTower;
            string selectedText = selectedTower == null
                ? "Selected: None"
                : $"Selected: {selectedTower.CombatDefinition.Core.DisplayName} "
                    + $"({selectedTower.CombatDefinition.NetworkRole})";
            string chainText = $"Valid chains: {towerNetworkSystem.ValidChainCount}"
                + $"   Towers: {towerNetworkSystem.RegisteredTowerCount}";
            string queueText = CreateQueueText(towerNetworkSystem, selectedTower);
            string feedbackText = string.IsNullOrWhiteSpace(towerNetworkSystem.LastFeedback)
                ? "Place towers, then drag one tower to another."
                : towerNetworkSystem.LastFeedback;
            bool simulationRunning = towerNetworkSystem.IsRunning;

            towerNetworkHud.Render(new TowerNetworkHudState(
                selectedText,
                chainText,
                queueText,
                feedbackText,
                !simulationRunning,
                selectedTower != null && towerNetworkSystem.CanEditTopology,
                !simulationRunning));
        }

        private void HandleTowerDragBegan(
            TowerCombatDefinition definition,
            TowerPlacementPointerEvent pointerEvent)
        {
            towerNetworkSystem.BeginTowerPlacementDrag(
                definition,
                pointerEvent.PointerId);
        }

        private void HandleTowerDragMoved(TowerPlacementPointerEvent pointerEvent)
        {
            towerNetworkSystem.UpdateTowerPlacementDrag(
                pointerEvent.PointerId,
                pointerEvent.ScreenPosition,
                pointerEvent.IsOverUi);
        }

        private void HandleTowerDragEnded(TowerPlacementPointerEvent pointerEvent)
        {
            towerNetworkSystem.EndTowerPlacementDrag(
                pointerEvent.PointerId,
                pointerEvent.ScreenPosition,
                pointerEvent.IsOverUi);
        }

        private void HandleTowerDragCanceled(int pointerId)
        {
            towerNetworkSystem.CancelTowerPlacementDrag(pointerId);
        }

        private void HandleCancelPlacement()
        {
            towerNetworkSystem.CancelPlacement();
            Refresh();
        }

        private void HandleUnlinkRequested()
        {
            towerNetworkSystem.TryUnlinkSelected(out _);
        }

        private void HandleReturnToMenu()
        {
            towerNetworkSystem.CancelPlacement();
            requestReturnToMenu();
        }

        private static string CreateQueueText(
            TowerNetworkSystem system,
            ITowerRuntimeView selectedTower)
        {
            if (selectedTower == null)
            {
                return "Queue: select a tower";
            }

            if (!system.TryCreateSelectedQueueSummary(out TowerQueueSummary queue))
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
