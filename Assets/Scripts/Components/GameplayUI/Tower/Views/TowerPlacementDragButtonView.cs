using System;
using TowerDefense3D.Towers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class TowerPlacementDragButtonView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private static readonly Color LockedTint = new Color(0.34f, 0.35f, 0.38f, 0.94f);
        private static readonly Color LockedTextColor = new Color(0.58f, 0.60f, 0.64f, 1f);

        [SerializeField] private Button button;
        [SerializeField] private TowerCombatDefinition definition;
        [SerializeField] private Text nameText;
        [SerializeField] private Text costText;
        private int activePointerId;
        private bool isDragging;
        private bool isLocked;
        private Color unlockedBackgroundColor;
        private Color unlockedNameColor;
        private Color unlockedCostColor;
        private bool hasCachedUnlockedColors;

        public event Action<TowerCombatDefinition, TowerPlacementPointerEvent> DragBegan;
        public event Action<TowerPlacementPointerEvent> DragMoved;
        public event Action<TowerPlacementPointerEvent> DragEnded;
        public event Action<int> DragCanceled;

        public Button Button => button;
        public TowerCombatDefinition Definition => definition;
        public bool IsLocked => isLocked;

        public void ApplyDefinitionLabels()
        {
            if (definition == null)
            {
                return;
            }

            if (nameText != null)
            {
                nameText.text = definition.Core.DisplayName.ToUpperInvariant();
            }

            if (costText != null)
            {
                costText.text = isLocked
                    ? "LOCKED"
                    : definition.Core.Economy.BuildCost.ToString("N0");
            }
        }

        /// <summary>
        /// A locked tower keeps its slot in the build bar but goes flat grey and stops
        /// responding, so the player can see what is still to come without being able to drag it.
        /// </summary>
        public void SetLocked(bool locked)
        {
            CacheUnlockedColors();
            isLocked = locked;
            if (locked)
            {
                CancelActiveDrag();
                button.interactable = false;
            }

            ApplyLockedColors();
            ApplyDefinitionLabels();
        }

        public void SetInteractable(bool interactable)
        {
            bool allowed = interactable && !isLocked;
            if (!allowed)
            {
                CancelActiveDrag();
            }

            button.interactable = allowed;
        }

        private void CacheUnlockedColors()
        {
            if (hasCachedUnlockedColors)
            {
                return;
            }

            Image background = Background;
            unlockedBackgroundColor = background != null ? background.color : Color.white;
            unlockedNameColor = nameText != null ? nameText.color : Color.white;
            unlockedCostColor = costText != null ? costText.color : Color.white;
            hasCachedUnlockedColors = true;
        }

        private Image Background => button.targetGraphic as Image ?? GetComponent<Image>();

        private void ApplyLockedColors()
        {
            Image background = Background;
            if (background != null)
            {
                background.color = isLocked ? LockedTint : unlockedBackgroundColor;
            }

            if (nameText != null)
            {
                nameText.color = isLocked ? LockedTextColor : unlockedNameColor;
            }

            if (costText != null)
            {
                costText.color = isLocked ? LockedTextColor : unlockedCostColor;
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (isLocked || !button.IsInteractable())
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

        private void Awake()
        {
            ApplyDefinitionLabels();
        }

        private void OnDisable()
        {
            CancelActiveDrag();
        }
    }
}
