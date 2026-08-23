using System;
using TowerDefense3D.Towers;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class TowerNetworkHudView : MonoBehaviour
    {
        [SerializeField] private TowerPlacementDragButton[] towerDragButtons =
            Array.Empty<TowerPlacementDragButton>();
        [SerializeField] private Button unlinkButton;
        [SerializeField] private Button startWaveButton;
        [SerializeField] private Button cancelPlacementButton;
        [SerializeField] private Button returnToMenuButton;
        [SerializeField] private Text selectedText;
        [SerializeField] private Text chainText;
        [SerializeField] private Text queueText;
        [SerializeField] private Text feedbackText;

        private bool isInitialized;

        public event Action<TowerCombatDefinition, TowerPlacementPointerEvent> TowerDragBegan;
        public event Action<TowerPlacementPointerEvent> TowerDragMoved;
        public event Action<TowerPlacementPointerEvent> TowerDragEnded;
        public event Action<int> TowerDragCanceled;
        public event Action UnlinkRequested;
        public event Action StartWaveRequested;
        public event Action CancelPlacementRequested;
        public event Action ReturnToMenuRequested;

        public bool IsInitialized => isInitialized;

        public void Initialize()
        {
            Shutdown();
            for (int index = 0; index < towerDragButtons.Length; index++)
            {
                TowerPlacementDragButton dragButton = towerDragButtons[index];
                dragButton.DragBegan += HandleTowerDragBegan;
                dragButton.DragMoved += HandleTowerDragMoved;
                dragButton.DragEnded += HandleTowerDragEnded;
                dragButton.DragCanceled += HandleTowerDragCanceled;
            }

            unlinkButton.onClick.AddListener(HandleUnlinkRequested);
            startWaveButton.onClick.AddListener(HandleStartWaveRequested);
            cancelPlacementButton.onClick.AddListener(HandleCancelPlacementRequested);
            returnToMenuButton.onClick.AddListener(HandleReturnToMenuRequested);
            isInitialized = true;
            gameObject.SetActive(true);
        }

        public void Render(TowerNetworkHudState state)
        {
            selectedText.text = state.SelectedText;
            chainText.text = state.ChainText;
            queueText.text = state.QueueText;
            feedbackText.text = state.FeedbackText;
            unlinkButton.interactable = state.UnlinkEnabled;
            startWaveButton.interactable = state.StartWaveEnabled;
            cancelPlacementButton.interactable = state.CancelPlacementEnabled;
            SetButtonLabel(startWaveButton, state.StartWaveText);

            for (int index = 0; index < towerDragButtons.Length; index++)
            {
                towerDragButtons[index].SetInteractable(state.TowerSelectionEnabled);
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
                TowerPlacementDragButton dragButton = towerDragButtons[index];
                dragButton.DragBegan -= HandleTowerDragBegan;
                dragButton.DragMoved -= HandleTowerDragMoved;
                dragButton.DragEnded -= HandleTowerDragEnded;
                dragButton.DragCanceled -= HandleTowerDragCanceled;
            }

            unlinkButton.onClick.RemoveListener(HandleUnlinkRequested);
            startWaveButton.onClick.RemoveListener(HandleStartWaveRequested);
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

        private void HandleUnlinkRequested()
        {
            UnlinkRequested?.Invoke();
        }

        private void HandleStartWaveRequested()
        {
            StartWaveRequested?.Invoke();
        }

        private void HandleCancelPlacementRequested()
        {
            CancelPlacementRequested?.Invoke();
        }

        private void HandleReturnToMenuRequested()
        {
            ReturnToMenuRequested?.Invoke();
        }

        private static void SetButtonLabel(Button button, string label)
        {
            button.GetComponentInChildren<Text>(true).text = label;
        }
    }
}
