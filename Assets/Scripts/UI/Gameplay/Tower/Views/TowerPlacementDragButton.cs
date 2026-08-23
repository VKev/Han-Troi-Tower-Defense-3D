using System;
using TowerDefense3D.Towers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow
{
    public readonly struct TowerPlacementPointerEvent
    {
        public TowerPlacementPointerEvent(int pointerId, Vector2 screenPosition, bool isOverUi)
        {
            PointerId = pointerId;
            ScreenPosition = screenPosition;
            IsOverUi = isOverUi;
        }

        public int PointerId { get; }
        public Vector2 ScreenPosition { get; }
        public bool IsOverUi { get; }
    }

    [DisallowMultipleComponent]
    public sealed class TowerPlacementDragButton : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private TowerCombatDefinition definition;
        private int activePointerId;
        private bool isDragging;

        public event Action<TowerCombatDefinition, TowerPlacementPointerEvent> DragBegan;
        public event Action<TowerPlacementPointerEvent> DragMoved;
        public event Action<TowerPlacementPointerEvent> DragEnded;
        public event Action<int> DragCanceled;

        public Button Button => button;
        public TowerCombatDefinition Definition => definition;

        public void SetInteractable(bool interactable)
        {
            if (!interactable)
            {
                CancelActiveDrag();
            }

            button.interactable = interactable;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!button.IsInteractable())
            {
                return;
            }

            activePointerId = eventData.pointerId;
            isDragging = true;
            DragBegan?.Invoke(definition, CreatePointerEvent(eventData));
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!MatchesActivePointer(eventData))
            {
                return;
            }

            DragMoved?.Invoke(CreatePointerEvent(eventData));
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!MatchesActivePointer(eventData))
            {
                return;
            }

            TowerPlacementPointerEvent pointerEvent = CreatePointerEvent(eventData);
            isDragging = false;
            activePointerId = 0;
            DragEnded?.Invoke(pointerEvent);
        }

        private bool MatchesActivePointer(PointerEventData eventData)
        {
            return isDragging && eventData.pointerId == activePointerId;
        }

        private void CancelActiveDrag()
        {
            if (!isDragging)
            {
                return;
            }

            int pointerId = activePointerId;
            isDragging = false;
            activePointerId = 0;
            DragCanceled?.Invoke(pointerId);
        }

        private static TowerPlacementPointerEvent CreatePointerEvent(PointerEventData eventData)
        {
            bool isOverUi = eventData.pointerCurrentRaycast.module is GraphicRaycaster;
            return new TowerPlacementPointerEvent(eventData.pointerId, eventData.position, isOverUi);
        }

        private void OnDisable()
        {
            CancelActiveDrag();
        }
    }
}
