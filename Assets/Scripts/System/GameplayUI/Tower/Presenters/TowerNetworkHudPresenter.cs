using System;
using TowerDefense3D.Towers;
using UnityEngine;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Converts tower-network state and HUD commands between the model and its view.
    /// </summary>
    public sealed class TowerNetworkHudPresenter
    {
        /// <summary>
        /// Lifts the action panel clear of the tower's own silhouette.
        /// </summary>
        private const float TowerActionsHeightMeters = 0.35f;

        private readonly TowerNetworkSystem towerNetworkSystem;
        private readonly ITowerNetworkHudView towerNetworkHud;
        private readonly Camera worldCamera;
        private Action requestReturnToMenu;

        public TowerNetworkHudPresenter(
            TowerNetworkSystem towerNetworkSystem,
            ITowerNetworkHudView towerNetworkHud,
            Camera worldCamera)
        {
            this.worldCamera = worldCamera;
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
            towerNetworkHud.SellRequested += HandleSellRequested;
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
            towerNetworkHud.SellRequested -= HandleSellRequested;
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
            bool towerActionsVisible = TryGetTowerActionsPosition(
                selectedTower,
                out Vector2 towerActionsScreenPosition);

            towerNetworkHud.Render(new TowerNetworkHudState(
                selectedText,
                chainText,
                queueText,
                feedbackText,
                !simulationRunning,
                selectedTower != null && towerNetworkSystem.CanEditTopology,
                selectedTower != null && towerNetworkSystem.CanEditTopology,
                !simulationRunning,
                towerActionsVisible,
                towerActionsScreenPosition));
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

        /// <summary>
        /// Projects the selected tower's anchor to screen space. A tower behind the camera
        /// projects to a mirrored point, so the negative depth case hides the panel instead.
        /// </summary>
        private bool TryGetTowerActionsPosition(
            ITowerRuntimeView selectedTower,
            out Vector2 screenPosition)
        {
            screenPosition = default;
            if (selectedTower == null || worldCamera == null)
            {
                return false;
            }

            Vector3 anchor = selectedTower.PresentationAnchor
                + Vector3.up * TowerActionsHeightMeters;
            Vector3 projected = worldCamera.WorldToScreenPoint(anchor);
            if (projected.z <= 0f)
            {
                return false;
            }

            screenPosition = new Vector2(projected.x, projected.y);
            return true;
        }

        private void HandleSellRequested()
        {
            towerNetworkSystem.TrySellSelected(out _);
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
