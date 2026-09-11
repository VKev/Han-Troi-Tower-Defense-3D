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

        [Tooltip("Dims the whole card while its price is out of reach. This cannot be done the way the locked look is: that comes from the Button's disabledColor, and an unaffordable card stays pressable so the HUD can answer the tap that cannot be paid for.")]
        [SerializeField] private CanvasGroup affordabilityGroup;

        [SerializeField, Range(0.1f, 1f)] private float unaffordableAlpha = 0.45f;

        private int activePointerId;
        private bool isDragging;
        private bool isLocked;
        private bool isAffordable = true;

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
            ApplyAffordability();
            ApplyDefinitionLabels();
        }

        /// <summary>
        /// Dims the card when the player cannot pay for it, while leaving it pressable.
        /// </summary>
        /// <remarks>
        /// Pressable on purpose. A card that went dead would take the tap with it, and the tap is
        /// what the status HUD answers with its shake and its red balance - the player would be
        /// told nothing at all rather than told why.
        /// </remarks>
        public void SetAffordable(bool affordable)
        {
            isAffordable = affordable;
            ApplyAffordability();
        }

        private void ApplyAffordability()
        {
            if (affordabilityGroup == null)
            {
                return;
            }

            // A locked card already reads as unavailable through its padlock and disabled tint,
            // so the price dim only speaks while the card is otherwise being offered.
            affordabilityGroup.alpha = !isLocked && !isAffordable ? unaffordableAlpha : 1f;
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
