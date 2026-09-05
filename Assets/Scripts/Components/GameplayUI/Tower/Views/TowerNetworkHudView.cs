using System;
using System.Collections.Generic;
using TowerDefense3D.Towers;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class TowerNetworkHudView : MonoBehaviour, ITowerNetworkHudView
    {
        [SerializeField] private TowerPlacementDragButtonView[] towerDragButtons =
            Array.Empty<TowerPlacementDragButtonView>();
        [SerializeField] private Button unlinkButton;
        [SerializeField] private Button sellButton;

        [Tooltip("Buys the selected tower a level. Its label is the price, or MAX when the tower has no level left.")]
        [SerializeField] private Button upgradeButton;

        [Tooltip("Price printed on the upgrade button.")]
        [SerializeField] private Text upgradeCostText;

        [Tooltip("Coin beside that price. Hidden along with it, because a coin next to nothing - or next to MAX - reads as a price that is missing rather than one that does not exist.")]
        [SerializeField] private GameObject upgradeCostIcon;

        [Tooltip("Refund printed on the sell button.")]
        [SerializeField] private Text sellRefundText;
        [Tooltip("Panel holding the per-tower actions, moved over the selected tower each frame.")]
        [SerializeField] private RectTransform towerActionsPanel;
        [Tooltip("Optional. The HUD's own menu button is gone - the pause modal carries that command now - so this is left unwired unless a screen puts one back.")]
        [SerializeField] private Button returnToMenuButton;

        private bool isInitialized;
        private Canvas rootCanvas;

        public event Action<TowerCombatDefinition, TowerPlacementPointerEvent> TowerDragBegan;
        public event Action<TowerPlacementPointerEvent> TowerDragMoved;
        public event Action<TowerPlacementPointerEvent> TowerDragEnded;
        public event Action<int> TowerDragCanceled;
        public event Action UnlinkRequested;
        public event Action SellRequested;
        public event Action UpgradeRequested;
        public event Action ReturnToMenuRequested;

        public bool IsInitialized => isInitialized;

        public void Initialize()
        {
            if (isInitialized)
            {
                return;
            }

            for (int index = 0; index < towerDragButtons.Length; index++)
            {
                TowerPlacementDragButtonView dragButton = towerDragButtons[index];
                dragButton.DragBegan += HandleTowerDragBegan;
                dragButton.DragMoved += HandleTowerDragMoved;
                dragButton.DragEnded += HandleTowerDragEnded;
                dragButton.DragCanceled += HandleTowerDragCanceled;
            }

            rootCanvas = GetComponentInParent<Canvas>();
            unlinkButton.onClick.AddListener(HandleUnlinkRequested);
            sellButton.onClick.AddListener(HandleSellRequested);
            if (upgradeButton != null)
            {
                upgradeButton.onClick.AddListener(HandleUpgradeRequested);
            }

            if (returnToMenuButton != null)
            {
                returnToMenuButton.onClick.AddListener(HandleReturnToMenuRequested);
            }

            isInitialized = true;
        }

        public void ApplyTowerLocks(IReadOnlyList<TowerCombatDefinition> lockedDefinitions)
        {
            for (int index = 0; index < towerDragButtons.Length; index++)
            {
                TowerPlacementDragButtonView dragButton = towerDragButtons[index];
                dragButton.SetLocked(Contains(lockedDefinitions, dragButton.Definition));
            }
        }

        private static bool Contains(
            IReadOnlyList<TowerCombatDefinition> definitions,
            TowerCombatDefinition definition)
        {
            if (definitions == null || definition == null)
            {
                return false;
            }

            for (int index = 0; index < definitions.Count; index++)
            {
                if (definitions[index] == definition)
                {
                    return true;
                }
            }

            return false;
        }

        public void Render(TowerNetworkHudState state)
        {
            unlinkButton.interactable = state.UnlinkEnabled;
            sellButton.interactable = state.SellEnabled;
            if (upgradeButton != null)
            {
                upgradeButton.interactable = state.UpgradeEnabled;
            }

            if (upgradeCostText != null)
            {
                upgradeCostText.text = state.UpgradeCostText;
            }

            if (upgradeCostIcon != null)
            {
                upgradeCostIcon.SetActive(state.UpgradeShowsPrice);
            }

            if (sellRefundText != null)
            {
                sellRefundText.text = state.SellRefundText;
            }

            RenderTowerActions(state);

            for (int index = 0; index < towerDragButtons.Length; index++)
            {
                towerDragButtons[index].SetInteractable(state.TowerSelectionEnabled);
            }
        }

        /// <summary>
        /// Drives the floating action panel. The panel is parented into the HUD, so the tower's
        /// screen point has to be converted into its parent's local space rather than assigned
        /// as a raw screen coordinate.
        /// </summary>
        private void RenderTowerActions(TowerNetworkHudState state)
        {
            if (towerActionsPanel == null)
            {
                return;
            }

            if (!state.TowerActionsVisible)
            {
                if (towerActionsPanel.gameObject.activeSelf)
                {
                    towerActionsPanel.gameObject.SetActive(false);
                }

                return;
            }

            if (!towerActionsPanel.gameObject.activeSelf)
            {
                towerActionsPanel.gameObject.SetActive(true);
            }

            if (!(towerActionsPanel.parent is RectTransform parent))
            {
                return;
            }

            Camera uiCamera = rootCanvas != null
                && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? rootCanvas.worldCamera
                    : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent,
                    state.TowerActionsScreenPosition,
                    uiCamera,
                    out Vector2 localPoint))
            {
                towerActionsPanel.anchoredPosition = localPoint;
            }
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void Shutdown()
        {
            if (!isInitialized)
            {
                return;
            }

            for (int index = 0; index < towerDragButtons.Length; index++)
            {
                TowerPlacementDragButtonView dragButton = towerDragButtons[index];
                dragButton.DragBegan -= HandleTowerDragBegan;
                dragButton.DragMoved -= HandleTowerDragMoved;
                dragButton.DragEnded -= HandleTowerDragEnded;
                dragButton.DragCanceled -= HandleTowerDragCanceled;
            }

            unlinkButton.onClick.RemoveListener(HandleUnlinkRequested);
            sellButton.onClick.RemoveListener(HandleSellRequested);
            if (upgradeButton != null)
            {
                upgradeButton.onClick.RemoveListener(HandleUpgradeRequested);
            }

            if (returnToMenuButton != null)
            {
                returnToMenuButton.onClick.RemoveListener(HandleReturnToMenuRequested);
            }

            isInitialized = false;
        }

        private void HandleTowerDragBegan(
            TowerCombatDefinition definition,
            TowerPlacementPointerEvent pointerEvent)
        {
            TowerDragBegan?.Invoke(definition, pointerEvent);
        }

        private void HandleTowerDragMoved(TowerPlacementPointerEvent pointerEvent)
        {
            TowerDragMoved?.Invoke(pointerEvent);
        }

        private void HandleTowerDragEnded(TowerPlacementPointerEvent pointerEvent)
        {
            TowerDragEnded?.Invoke(pointerEvent);
        }

        private void HandleTowerDragCanceled(int pointerId)
        {
            TowerDragCanceled?.Invoke(pointerId);
        }

        private void HandleSellRequested()
        {
            SellRequested?.Invoke();
        }

        private void HandleUpgradeRequested()
        {
            UpgradeRequested?.Invoke();
        }

        private void HandleUnlinkRequested()
        {
            UnlinkRequested?.Invoke();
        }

        private void HandleReturnToMenuRequested()
        {
            ReturnToMenuRequested?.Invoke();
        }

    }
}
