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
        [Tooltip("Panel holding the per-tower actions, moved over the selected tower each frame.")]
        [SerializeField] private RectTransform towerActionsPanel;
        [SerializeField] private Button cancelPlacementButton;
        [SerializeField] private Button returnToMenuButton;
        [SerializeField] private Text selectedText;
        [SerializeField] private Text chainText;
        [SerializeField] private Text queueText;
        [SerializeField] private Text feedbackText;

        private bool isInitialized;
        private Canvas rootCanvas;

        public event Action<TowerCombatDefinition, TowerPlacementPointerEvent> TowerDragBegan;
        public event Action<TowerPlacementPointerEvent> TowerDragMoved;
        public event Action<TowerPlacementPointerEvent> TowerDragEnded;
        public event Action<int> TowerDragCanceled;
        public event Action UnlinkRequested;
        public event Action SellRequested;
        public event Action CancelPlacementRequested;
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
            cancelPlacementButton.onClick.AddListener(HandleCancelPlacementRequested);
            returnToMenuButton.onClick.AddListener(HandleReturnToMenuRequested);
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
            selectedText.text = state.SelectedText;
            chainText.text = state.ChainText;
            queueText.text = state.QueueText;
            feedbackText.text = state.FeedbackText;
            unlinkButton.interactable = state.UnlinkEnabled;
            sellButton.interactable = state.SellEnabled;
            RenderTowerActions(state);
            cancelPlacementButton.interactable = state.CancelPlacementEnabled;

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
            cancelPlacementButton.onClick.RemoveListener(HandleCancelPlacementRequested);
            returnToMenuButton.onClick.RemoveListener(HandleReturnToMenuRequested);
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

        private void HandleUnlinkRequested()
        {
            UnlinkRequested?.Invoke();
        }

        private void HandleCancelPlacementRequested()
        {
            CancelPlacementRequested?.Invoke();
        }

        private void HandleReturnToMenuRequested()
        {
            ReturnToMenuRequested?.Invoke();
        }

    }
}
