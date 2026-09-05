using System;
using System.Collections.Generic;
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
        private readonly TowerCatalog towerCatalog;
        private readonly SaveSystem saveSystem;
        private Action requestReturnToMenu;

        public TowerNetworkHudPresenter(
            TowerNetworkSystem towerNetworkSystem,
            ITowerNetworkHudView towerNetworkHud,
            Camera worldCamera,
            TowerCatalog towerCatalog,
            SaveSystem saveSystem)
        {
            this.worldCamera = worldCamera;
            this.towerCatalog = towerCatalog ?? throw new ArgumentNullException(nameof(towerCatalog));
            this.saveSystem = saveSystem ?? throw new ArgumentNullException(nameof(saveSystem));
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
            towerNetworkHud.ApplyTowerLocks(CollectLockedDefinitions());
            towerNetworkHud.TowerDragBegan += HandleTowerDragBegan;
            towerNetworkHud.TowerDragMoved += HandleTowerDragMoved;
            towerNetworkHud.TowerDragEnded += HandleTowerDragEnded;
            towerNetworkHud.TowerDragCanceled += HandleTowerDragCanceled;
            towerNetworkHud.UnlinkRequested += HandleUnlinkRequested;
            towerNetworkHud.SellRequested += HandleSellRequested;
            towerNetworkHud.UpgradeRequested += HandleUpgradeRequested;
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
            towerNetworkHud.UpgradeRequested -= HandleUpgradeRequested;
            towerNetworkHud.ReturnToMenuRequested -= HandleReturnToMenu;
        }

        public void Refresh()
        {
            ITowerRuntimeView selectedTower = towerNetworkSystem.SelectedTower;
            string selectedText = selectedTower == null
                ? "Selected: None"
                : $"Selected: {selectedTower.CombatDefinition.Core.DisplayName} "
                    + $"({selectedTower.CombatDefinition.NetworkRole})";
            string feedbackText = string.IsNullOrWhiteSpace(towerNetworkSystem.LastFeedback)
                ? "Place towers, then drag one tower to another."
                : towerNetworkSystem.LastFeedback;
            bool simulationRunning = towerNetworkSystem.IsRunning;
            bool towerActionsVisible = TryGetTowerActionsPosition(
                selectedTower,
                out Vector2 towerActionsScreenPosition);
            bool canEdit = towerNetworkSystem.CanEditTopology;
            bool hasUpgrade = towerNetworkSystem.TryDescribeSelectedUpgrade(
                out int upgradeCost,
                out bool affordable,
                out bool atMaxLevel);

            towerNetworkHud.Render(new TowerNetworkHudState(
                selectedText,
                feedbackText,
                !simulationRunning,
                selectedTower != null && towerNetworkSystem.CanEditTopology,
                selectedTower != null && towerNetworkSystem.CanEditTopology,
                towerActionsVisible,
                towerActionsScreenPosition,
                canEdit && hasUpgrade && !atMaxLevel && affordable,
                CreateUpgradeCostText(hasUpgrade, atMaxLevel, upgradeCost),
                towerNetworkSystem.DescribeSelectedSellRefund().ToString(),
                hasUpgrade && !atMaxLevel));
        }

        /// <summary>
        /// A tower stays locked until the player has actually beaten the level it is gated on.
        /// Unlocking is evaluated once per level entry: progress cannot change mid-level, and a
        /// tower unlocked here would otherwise appear the instant its own level is won.
        /// </summary>
        private IReadOnlyList<TowerCombatDefinition> CollectLockedDefinitions()
        {
            var locked = new List<TowerCombatDefinition>();
            IReadOnlyList<TowerCombatDefinition> definitions = towerCatalog.Definitions;
            for (int index = 0; index < definitions.Count; index++)
            {
                TowerCombatDefinition definition = definitions[index];
                int requiredLevel = definition == null ? 0 : definition.UnlockAfterClearingLevelNumber;
                if (requiredLevel > 0 && !saveSystem.Progress.IsCleared(requiredLevel))
                {
                    locked.Add(definition);
                }
            }

            return locked;
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

        /// <summary>
        /// Sells the selected tower, and says so when it refuses.
        /// </summary>
        /// <remarks>
        /// The HUD lost its feedback line, so a refusal used to be dropped on the floor: the
        /// button looked dead rather than declined, which is indistinguishable from a broken
        /// button. Until there is somewhere on screen to print it, the reason goes to the log
        /// so a refusal is at least diagnosable instead of invisible.
        /// </remarks>
        /// <summary>
        /// What the upgrade button says: a price, or MAX once the tower has no level left to buy.
        /// </summary>
        /// <remarks>
        /// Printing the price of a level that cannot be bought would read as a purchase the
        /// player merely cannot afford, which is a different problem with a different fix.
        /// </remarks>
        private static string CreateUpgradeCostText(bool hasUpgrade, bool atMaxLevel, int cost)
        {
            if (!hasUpgrade)
            {
                return string.Empty;
            }

            return atMaxLevel ? "MAX" : cost.ToString();
        }

        private void HandleUpgradeRequested()
        {
            if (!towerNetworkSystem.TryUpgradeSelected(out string error))
            {
                Debug.LogWarning("Upgrade refused: " + error);
            }
        }

        private void HandleSellRequested()
        {
            if (!towerNetworkSystem.TrySellSelected(out string error))
            {
                Debug.LogWarning("Sell refused: " + error);
            }
        }

        private void HandleUnlinkRequested()
        {
            if (!towerNetworkSystem.TryUnlinkSelected(out string error))
            {
                Debug.LogWarning("Unlink refused: " + error);
            }
        }

        private void HandleReturnToMenu()
        {
            towerNetworkSystem.CancelPlacement();
            requestReturnToMenu();
        }

    }
}
