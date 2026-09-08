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
        private bool tutorialControlsVisible = true;
        private Canvas rootCanvas;
        private GameObject buildBar;
        private GameObject towerButtons;

        public event Action<TowerCombatDefinition, TowerPlacementPointerEvent> TowerDragBegan;
        public event Action<TowerPlacementPointerEvent> TowerDragMoved;
        public event Action<TowerPlacementPointerEvent> TowerDragEnded;
        public event Action<int> TowerDragCanceled;
        public event Action UnlinkRequested;
        public event Action SellRequested;
        public event Action UpgradeRequested;
        public event Action ReturnToMenuRequested;

        public bool IsInitialized => isInitialized;

        /// <summary>
        /// The colour each graphic on an action button was authored with, so a tint can be applied
        /// over it rather than replacing it.
        /// </summary>
        private readonly Dictionary<Graphic, Color> authoredButtonColors = new();

        public void Initialize()
        {
            if (isInitialized)
            {
                return;
            }

            RememberButtonColors(unlinkButton);
            RememberButtonColors(sellButton);
            RememberButtonColors(upgradeButton);

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

            // Unity's ColorTint reaches only the one graphic a Button targets - its plate - so the
            // arrow, the coin and the price stayed at full brightness on a greyed button and read
            // as a control that was still live.
            TintButtonContents(unlinkButton, state.UnlinkEnabled);
            TintButtonContents(sellButton, state.SellEnabled);
            TintButtonContents(upgradeButton, state.UpgradeEnabled);

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

            if (!tutorialControlsVisible || !state.TowerActionsVisible)
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

        public void SetTutorialControlsVisible(bool visible)
        {
            tutorialControlsVisible = visible;
            buildBar ??= transform.Find("Build Bar")?.gameObject;
            towerButtons ??= transform.Find("Tower Buttons")?.gameObject;
            if (buildBar != null) buildBar.SetActive(visible);
            if (towerButtons != null) towerButtons.SetActive(visible);
            for (int index = 0; index < towerDragButtons.Length; index++)
            {
                TowerPlacementDragButtonView dragButton = towerDragButtons[index];
                if (dragButton != null) dragButton.gameObject.SetActive(visible);
            }

            if (!visible && towerActionsPanel != null)
            {
                towerActionsPanel.gameObject.SetActive(false);
            }
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

        private void RememberButtonColors(Button button)
        {
            if (button == null)
            {
                return;
            }

            Graphic[] graphics = button.GetComponentsInChildren<Graphic>(true);
            for (int index = 0; index < graphics.Length; index++)
            {
                if (graphics[index] == button.targetGraphic)
                {
                    continue;
                }

                authoredButtonColors[graphics[index]] = graphics[index].color;
            }
        }

        /// <summary>
        /// Dims everything drawn on a button in step with the button itself.
        /// </summary>
        /// <remarks>
        /// The tint is the Button's own normal and disabled colours, multiplied over what each
        /// graphic was authored with rather than replacing it - which is exactly what Unity does
        /// to the plate. Multiplying is what keeps the cost band's own dark, half-transparent
        /// wash from being flattened to a flat grey the moment the button goes dead.
        ///
        /// The plate is skipped: the Button is already driving that one, and tinting it here as
        /// well would apply the disabled colour twice.
        /// </remarks>
        private void TintButtonContents(Button button, bool isEnabled)
        {
            if (button == null)
            {
                return;
            }

            ColorBlock colors = button.colors;
            Color tint = isEnabled ? colors.normalColor : colors.disabledColor;

            Graphic[] graphics = button.GetComponentsInChildren<Graphic>(true);
            for (int index = 0; index < graphics.Length; index++)
            {
                Graphic graphic = graphics[index];
                if (graphic == button.targetGraphic)
                {
                    continue;
                }

                if (!authoredButtonColors.TryGetValue(graphic, out Color authored))
                {
                    authored = graphic.color;
                    authoredButtonColors[graphic] = authored;
                }

                graphic.color = authored * tint;
            }
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
