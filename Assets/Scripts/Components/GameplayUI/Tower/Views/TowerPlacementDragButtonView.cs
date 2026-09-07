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
        [SerializeField] private Button button;
        [SerializeField] private TowerCombatDefinition definition;
        [SerializeField] private Text nameText;
        [SerializeField] private Text costText;

        [Tooltip("The tower's element icon. The padlock replaces it outright while locked.")]
        [SerializeField] private Image iconImage;

        [Tooltip("The coin pip beside the cost. A locked tower quotes no price, so it is hidden.")]
        [SerializeField] private Image coinImage;

        [Tooltip("The dark band the cost row sits in. It goes with the row it exists for.")]
        [SerializeField] private Image costShade;

        [Tooltip("The padlock. It becomes the tile's icon while the tower is locked.")]
        [SerializeField] private GameObject lockBadge;

        private int activePointerId;
        private bool isDragging;
        private bool isLocked;

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

            // The word "LOCKED" used to live here; the padlock says it now, and the whole
            // cost row is hidden while locked, so the text only ever carries the price.
            if (costText != null)
            {
                costText.text = definition.Core.Economy.BuildCost.ToString("N0");
            }
        }

        /// <summary>
        /// A locked tower keeps its slot in the build bar so the player can see what is still to
        /// come, but it stops responding and strips back to a single padlock: no element icon, no
        /// price, no coin, and none of the dark band that price sat in.
        /// </summary>
        /// <remarks>
        /// The tile's own darkening is not done here. The Button transitions by ColorTint and
        /// overwrites its target graphic's colour whenever interactable changes, so anything set
        /// from this script would be clobbered a frame later. The locked look therefore lives in
        /// the Button's disabledColor, which multiplies the tile and so keeps its hue.
        /// </remarks>
        public void SetLocked(bool locked)
        {
            isLocked = locked;
            if (locked)
            {
                CancelActiveDrag();
                button.interactable = false;
            }

            ApplyLockedVisibility();
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

        /// <summary>Everything the locked state shows or hides.</summary>
        private void ApplyLockedVisibility()
        {
            if (iconImage != null)
            {
                iconImage.enabled = !isLocked;
            }

            // The price, its coin and the band behind them are one unit: a locked tower shows
            // none of it rather than greying it in place.
            if (nameText != null)
            {
                nameText.enabled = !isLocked;
            }

            if (costText != null)
            {
                costText.enabled = !isLocked;
            }

            if (coinImage != null)
            {
                coinImage.enabled = !isLocked;
            }

            if (costShade != null)
            {
                costShade.enabled = !isLocked;
            }

            if (lockBadge != null)
            {
                lockBadge.SetActive(isLocked);
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
