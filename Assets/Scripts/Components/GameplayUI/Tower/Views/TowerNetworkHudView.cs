using System;
using System.Collections.Generic;
using DG.Tweening;
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
        [SerializeField] private LayoutElement tutorialGeneratorLayoutElement;
        [SerializeField] private CanvasGroup tutorialGeneratorCanvasGroup;
        private Tween tutorialGeneratorRevealTween;
        private bool tutorialGeneratorCardShown;
        private bool tutorialGeneratorLocked;
        [SerializeField] private CanvasGroup tutorialSinkCanvasGroup;
        private Tween tutorialSinkRevealTween;
        private bool tutorialSinkCardShown;
        private bool tutorialSoulNexusLocked;
        private Tween tutorialElementsRevealTween;
        private bool tutorialElementsShown;
        private bool tutorialFireLocked;
        private bool tutorialUnlinkOnly;
        private bool towerActionsAvailable;
        private bool heroLocked;
        private Transform heroCard;
        private bool waterWasLocked = true;
        private Tween waterUnlockTween;

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
            ValidateTutorialHelpers();
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

        private void ValidateTutorialHelpers()
        {
            Transform generatorCard = GetTowerButtonTransform(TowerFamily.Generator);
            Transform sinkCard = GetTowerButtonTransform(TowerFamily.SoulNexus);
            Transform generatorButton = GetTowerDragButtonTransform(TowerFamily.Generator);
            if (tutorialGeneratorCanvasGroup == null
                || generatorCard == null
                || tutorialGeneratorCanvasGroup.gameObject != generatorCard.gameObject)
            {
                throw new MissingReferenceException(
                    "TowerNetworkHudView requires Group Sources to own the Generator tutorial CanvasGroup.");
            }

            if (tutorialSinkCanvasGroup == null
                || sinkCard == null
                || tutorialSinkCanvasGroup.gameObject != sinkCard.gameObject)
            {
                throw new MissingReferenceException(
                    "TowerNetworkHudView requires Group Sinks to own the Sink tutorial CanvasGroup.");
            }

            if (tutorialGeneratorLayoutElement == null
                || generatorButton == null
                || tutorialGeneratorLayoutElement.gameObject != generatorButton.gameObject)
            {
                throw new MissingReferenceException(
                    "TowerNetworkHudView requires an authored Generator tutorial LayoutElement.");
            }
        }

        public void ApplyTowerLocks(IReadOnlyList<TowerCombatDefinition> lockedDefinitions)
        {
            for (int index = 0; index < towerDragButtons.Length; index++)
            {
                TowerPlacementDragButtonView dragButton = towerDragButtons[index];
                bool locked = Contains(lockedDefinitions, dragButton.Definition);
                dragButton.SetLocked(locked);
                if (dragButton.Definition?.Family == TowerFamily.Hero)
                {
                    heroLocked = locked;
                    heroCard ??= GetTowerButtonTransform(TowerFamily.Hero);
                    if (heroCard != null)
                    {
                        heroCard.gameObject.SetActive(!locked);
                    }
                }
                if (dragButton.Definition?.Family == TowerFamily.Water)
                {
                    if (waterWasLocked && !locked)
                    {
                        PlayWaterUnlock(dragButton.transform as RectTransform);
                    }

                    waterWasLocked = locked;
                }
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

        public void SetTowerActionsAvailable(bool available)
        {
            towerActionsAvailable = available;
            if (!available && towerActionsPanel != null)
            {
                towerActionsPanel.gameObject.SetActive(false);
            }
        }

        private void PlayWaterUnlock(RectTransform card)
        {
            if (card == null)
            {
                return;
            }

            waterUnlockTween?.Kill();
            card.localScale = Vector3.one * 0.72f;
            waterUnlockTween = card
                .DOScale(1f, 0.42f)
                .SetEase(Ease.OutBack)
                .SetUpdate(true)
                .SetTarget(this);
        }

        public void Render(TowerNetworkHudState state)
        {
            bool tutorialUnlinkEnabled = tutorialUnlinkOnly
                && state.SelectedTowerFamily == TowerFamily.Generator;
            bool unlinkEnabled = state.UnlinkEnabled || tutorialUnlinkEnabled;
            unlinkButton.interactable = unlinkEnabled;
            sellButton.interactable = state.SellEnabled;
            if (upgradeButton != null)
            {
                upgradeButton.interactable = state.UpgradeEnabled;
            }

            // Unity's ColorTint reaches only the one graphic a Button targets - its plate - so the
            // arrow, the coin and the price stayed at full brightness on a greyed button and read
            // as a control that was still live.
            TintButtonContents(unlinkButton, unlinkEnabled);
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
                bool isTutorialLocked = tutorialGeneratorLocked
                    && towerDragButtons[index]?.Definition?.Family == TowerFamily.Generator
                    || tutorialSoulNexusLocked
                    && towerDragButtons[index]?.Definition?.Family == TowerFamily.SoulNexus
                    || tutorialFireLocked
                    && towerDragButtons[index]?.Definition?.Family == TowerFamily.Fire;
                towerDragButtons[index].SetInteractable(state.TowerSelectionEnabled && !isTutorialLocked);
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

            bool canShowUnlinkOnly = tutorialUnlinkOnly
                && state.SelectedTowerFamily == TowerFamily.Generator;
            if (!tutorialControlsVisible
                && !towerActionsAvailable
                || !state.TowerActionsVisible
                || tutorialUnlinkOnly && !canShowUnlinkOnly)
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

            unlinkButton.gameObject.SetActive(!tutorialUnlinkOnly || canShowUnlinkOnly);
            sellButton.gameObject.SetActive(!tutorialUnlinkOnly);
            if (upgradeButton != null) upgradeButton.gameObject.SetActive(!tutorialUnlinkOnly);

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
            tutorialUnlinkOnly = false;
            towerActionsAvailable = visible;
            if (visible)
            {
                tutorialGeneratorLocked = false;
                tutorialSoulNexusLocked = false;
                tutorialFireLocked = false;
                unlinkButton.gameObject.SetActive(true);
                sellButton.gameObject.SetActive(true);
                if (upgradeButton != null) upgradeButton.gameObject.SetActive(true);
            }

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

            if (!visible)
            {
                tutorialGeneratorCardShown = false;
                tutorialGeneratorRevealTween?.Kill();
                tutorialGeneratorRevealTween = null;
                tutorialElementsShown = false;
                tutorialElementsRevealTween?.Kill();
                tutorialElementsRevealTween = null;
            }

            if (visible && towerButtons != null)
            {
                for (int index = 0; index < towerButtons.transform.childCount; index++)
                {
                    Transform child = towerButtons.transform.GetChild(index);
                    child.gameObject.SetActive(child != heroCard || !heroLocked);
                }

                HorizontalLayoutGroup row = towerButtons.GetComponent<HorizontalLayoutGroup>();
                if (row != null)
                {
                    row.childControlWidth = true;
                    row.childForceExpandWidth = true;
                    row.childAlignment = TextAnchor.MiddleCenter;
                }

                if (tutorialGeneratorLayoutElement != null)
                {
                    tutorialGeneratorLayoutElement.enabled = false;
                }
            }
        }

        public Transform GetTowerButtonTransform(TowerFamily family)
        {
            Transform buttonsRoot = GetTowerButtonsTransform();
            for (int index = 0; index < towerDragButtons.Length; index++)
            {
                TowerPlacementDragButtonView button = towerDragButtons[index];
                if (button?.Definition?.Family == family)
                {
                    for (Transform current = button.transform;
                         current != null && current.parent != null;
                         current = current.parent)
                    {
                        if (current.parent == buttonsRoot)
                        {
                            return current;
                        }
                    }

                    return button.transform;
                }
            }

            return null;
        }

        public Transform GetTowerDragButtonTransform(TowerFamily family)
        {
            for (int index = 0; index < towerDragButtons.Length; index++)
            {
                TowerPlacementDragButtonView button = towerDragButtons[index];
                if (button?.Definition?.Family == family)
                {
                    return button.transform;
                }
            }

            return null;
        }

        public Transform GetUnlinkButtonTransform()
        {
            return unlinkButton != null ? unlinkButton.transform : null;
        }

        public Transform GetUpgradeButtonTransform()
        {
            return upgradeButton != null ? upgradeButton.transform : null;
        }

        public void SetTutorialLevelTwoPlacement(TowerFamily family)
        {
            tutorialControlsVisible = false;
            tutorialUnlinkOnly = false;
            towerActionsAvailable = false;
            buildBar ??= transform.Find("Build Bar")?.gameObject;
            towerButtons ??= transform.Find("Tower Buttons")?.gameObject;
            gameObject.SetActive(true);
            if (buildBar != null) buildBar.SetActive(false);
            if (towerButtons != null)
            {
                towerButtons.SetActive(true);
                Transform selectedCard = GetTowerButtonTransform(family);
                for (int index = 0; index < towerButtons.transform.childCount; index++)
                {
                    Transform child = towerButtons.transform.GetChild(index);
                    child.gameObject.SetActive(child == selectedCard);
                }

                HorizontalLayoutGroup row = towerButtons.GetComponent<HorizontalLayoutGroup>();
                if (row != null)
                {
                    row.childControlWidth = true;
                    row.childForceExpandWidth = false;
                    row.childAlignment = TextAnchor.MiddleCenter;
                }

                LayoutRebuilder.ForceRebuildLayoutImmediate(towerButtons.transform as RectTransform);
            }

            for (int index = 0; index < towerDragButtons.Length; index++)
            {
                TowerPlacementDragButtonView button = towerDragButtons[index];
                bool selected = button?.Definition?.Family == family;
                if (button != null)
                {
                    button.gameObject.SetActive(selected);
                    button.SetLocked(!selected);
                    button.SetInteractable(selected);
                }
            }

            if (towerActionsPanel != null) towerActionsPanel.gameObject.SetActive(false);
        }

        public void SetTutorialLevelTwoLinking()
        {
            tutorialControlsVisible = false;
            tutorialUnlinkOnly = false;
            towerActionsAvailable = false;
            buildBar ??= transform.Find("Build Bar")?.gameObject;
            towerButtons ??= transform.Find("Tower Buttons")?.gameObject;
            gameObject.SetActive(true);
            if (buildBar != null) buildBar.SetActive(false);
            if (towerButtons != null) towerButtons.SetActive(false);
            for (int index = 0; index < towerDragButtons.Length; index++)
            {
                if (towerDragButtons[index] != null) towerDragButtons[index].gameObject.SetActive(false);
            }

            if (towerActionsPanel != null) towerActionsPanel.gameObject.SetActive(false);
        }

        public Transform GetTutorialHudTargetTransform()
        {
            towerButtons ??= transform.Find("Tower Buttons")?.gameObject;
            return towerButtons != null ? towerButtons.transform : transform;
        }

        public void SetTutorialGeneratorOnly()
        {
            tutorialControlsVisible = false;
            buildBar ??= transform.Find("Build Bar")?.gameObject;
            towerButtons ??= transform.Find("Tower Buttons")?.gameObject;
            gameObject.SetActive(true);
            if (buildBar != null) buildBar.SetActive(false);
            if (towerButtons != null)
            {
                towerButtons.SetActive(true);
                Transform generatorButton = GetTowerButtonTransform(TowerFamily.Generator);
                for (int index = 0; index < towerButtons.transform.childCount; index++)
                {
                    Transform child = towerButtons.transform.GetChild(index);
                    child.gameObject.SetActive(child == generatorButton);
                }

                HorizontalLayoutGroup row = towerButtons.GetComponent<HorizontalLayoutGroup>();
                if (row != null)
                {
                    row.childControlWidth = true;
                    row.childForceExpandWidth = false;
                    row.childAlignment = TextAnchor.MiddleCenter;
                }

            }

            for (int index = 0; index < towerDragButtons.Length; index++)
            {
                TowerPlacementDragButtonView button = towerDragButtons[index];
                bool isGenerator = button?.Definition?.Family == TowerFamily.Generator;
                if (button != null)
                {
                    button.gameObject.SetActive(isGenerator);
                    button.SetLocked(!isGenerator);
                    button.SetInteractable(isGenerator && !tutorialGeneratorLocked);
                    if (isGenerator && tutorialGeneratorLayoutElement != null)
                    {
                        tutorialGeneratorLayoutElement.enabled = true;
                        RectTransform buttonRect = button.transform as RectTransform;
                        float authoredWidth = buttonRect == null ? 0f : buttonRect.rect.width;
                        tutorialGeneratorLayoutElement.minWidth = authoredWidth > 1f ? authoredWidth : 180f;
                        tutorialGeneratorLayoutElement.preferredWidth =
                            tutorialGeneratorLayoutElement.minWidth;
                        tutorialGeneratorLayoutElement.flexibleWidth = 0f;
                    }
                }
            }

            if (towerButtons != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(towerButtons.transform as RectTransform);
                Transform generatorButton = GetTowerButtonTransform(TowerFamily.Generator);
                if (generatorButton != null && !tutorialGeneratorCardShown)
                {
                    RevealTutorialGeneratorCard(generatorButton as RectTransform);
                    tutorialGeneratorCardShown = true;
                }
            }

            if (towerActionsPanel != null)
            {
                towerActionsPanel.gameObject.SetActive(false);
            }
        }

        public void SetTutorialGeneratorPlaced()
        {
            tutorialGeneratorLocked = true;
            SetTutorialGeneratorOnly();
        }

        public void SetTutorialSinkPlacement()
        {
            SetTutorialGeneratorAndSink(false, true);
        }

        public void SetTutorialSecondGeneratorPlacement()
        {
            SetTutorialGeneratorAndSink(true, false);
        }

        public void SetTutorialGeneratorAndSinkReady()
        {
            SetTutorialGeneratorAndSink(true, true);
        }

        public void SetTutorialFirePlacement()
        {
            SetTutorialGeneratorSinkAndFire(true, true, true, true);
        }

        public void SetTutorialFireLink()
        {
            SetTutorialGeneratorSinkAndFire(false, false, false, true);
        }

        public void SetTutorialGeneratorUnlinkOnly()
        {
            if (tutorialUnlinkOnly)
            {
                return;
            }

            SetTutorialGeneratorSinkAndFire(false, false, false, true);
            tutorialControlsVisible = true;
            towerActionsAvailable = true;
            tutorialUnlinkOnly = true;
        }

        public void SetTutorialGeneratorSinkAndElementsReady()
        {
            SetTutorialGeneratorSinkAndFire(true, true, true, true);
        }

        private void SetTutorialGeneratorSinkAndFire(
            bool generatorInteractable,
            bool sinkInteractable,
            bool fireInteractable,
            bool showLockedElements)
        {
            tutorialControlsVisible = false;
            tutorialUnlinkOnly = false;
            towerActionsAvailable = false;
            tutorialGeneratorLocked = false;
            tutorialSoulNexusLocked = false;
            tutorialFireLocked = !fireInteractable;
            buildBar ??= transform.Find("Build Bar")?.gameObject;
            towerButtons ??= transform.Find("Tower Buttons")?.gameObject;
            gameObject.SetActive(true);
            if (buildBar != null) buildBar.SetActive(false);
            if (towerButtons == null)
            {
                return;
            }

            towerButtons.SetActive(true);
            Transform generatorCard = GetTowerButtonTransform(TowerFamily.Generator);
            Transform sinkCard = GetTowerButtonTransform(TowerFamily.SoulNexus);
            Transform elementsCard = GetTowerButtonTransform(TowerFamily.Fire);
            for (int index = 0; index < towerButtons.transform.childCount; index++)
            {
                Transform child = towerButtons.transform.GetChild(index);
                child.gameObject.SetActive(
                    child == generatorCard || child == sinkCard || child == elementsCard);
            }

            if (elementsCard != null && sinkCard != null)
            {
                elementsCard.SetSiblingIndex(Mathf.Min(
                    sinkCard.GetSiblingIndex() + 1,
                    towerButtons.transform.childCount - 1));
            }

            for (int index = 0; index < towerDragButtons.Length; index++)
            {
                TowerPlacementDragButtonView button = towerDragButtons[index];
                if (button == null)
                {
                    continue;
                }

                TowerFamily? family = button.Definition?.Family;
                bool isGenerator = family == TowerFamily.Generator;
                bool isSink = family == TowerFamily.SoulNexus;
                bool isFire = family == TowerFamily.Fire;
                bool isLockedElement = family == TowerFamily.Water || family == TowerFamily.Wind;
                bool isVisible = isGenerator || isSink || isFire
                    || (showLockedElements && isLockedElement);
                button.gameObject.SetActive(isVisible);
                button.SetLocked(isLockedElement || !isGenerator && !isSink && !isFire);
                button.SetInteractable(
                    isGenerator && generatorInteractable
                    || isSink && sinkInteractable
                    || isFire && fireInteractable);
            }

            HorizontalLayoutGroup row = towerButtons.GetComponent<HorizontalLayoutGroup>();
            if (row != null)
            {
                row.childControlWidth = true;
                row.childForceExpandWidth = false;
                row.childAlignment = TextAnchor.MiddleCenter;
            }

            if (tutorialGeneratorLayoutElement != null)
            {
                tutorialGeneratorLayoutElement.enabled = false;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(towerButtons.transform as RectTransform);
            RevealTutorialCards(generatorCard as RectTransform, sinkCard as RectTransform);
            RevealTutorialElements(elementsCard as RectTransform);
            if (towerActionsPanel != null) towerActionsPanel.gameObject.SetActive(false);
        }

        private void SetTutorialGeneratorAndSink(bool generatorInteractable, bool sinkInteractable)
        {
            tutorialControlsVisible = false;
            tutorialUnlinkOnly = false;
            towerActionsAvailable = false;
            tutorialGeneratorLocked = !generatorInteractable;
            tutorialSoulNexusLocked = !sinkInteractable;
            buildBar ??= transform.Find("Build Bar")?.gameObject;
            towerButtons ??= transform.Find("Tower Buttons")?.gameObject;
            gameObject.SetActive(true);
            if (buildBar != null) buildBar.SetActive(false);
            if (towerButtons == null)
            {
                return;
            }

            towerButtons.SetActive(true);
            Transform generatorCard = GetTowerButtonTransform(TowerFamily.Generator);
            Transform sinkCard = GetTowerButtonTransform(TowerFamily.SoulNexus);
            for (int index = 0; index < towerButtons.transform.childCount; index++)
            {
                Transform child = towerButtons.transform.GetChild(index);
                child.gameObject.SetActive(child == generatorCard || child == sinkCard);
            }

            for (int index = 0; index < towerDragButtons.Length; index++)
            {
                TowerPlacementDragButtonView button = towerDragButtons[index];
                if (button == null)
                {
                    continue;
                }

                bool isGenerator = button.Definition?.Family == TowerFamily.Generator;
                bool isSink = button.Definition?.Family == TowerFamily.SoulNexus;
                button.gameObject.SetActive(isGenerator || isSink);
                button.SetLocked(!isGenerator && !isSink);
                button.SetInteractable(isGenerator && generatorInteractable || isSink && sinkInteractable);
            }

            HorizontalLayoutGroup row = towerButtons.GetComponent<HorizontalLayoutGroup>();
            if (row != null)
            {
                row.childControlWidth = true;
                row.childForceExpandWidth = false;
                row.childAlignment = TextAnchor.MiddleCenter;
            }

            if (tutorialGeneratorLayoutElement != null)
            {
                tutorialGeneratorLayoutElement.enabled = false;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(towerButtons.transform as RectTransform);
            RevealTutorialCards(generatorCard as RectTransform, sinkCard as RectTransform);
            if (towerActionsPanel != null) towerActionsPanel.gameObject.SetActive(false);
        }

        private void RevealTutorialCards(RectTransform generatorCard, RectTransform sinkCard)
        {
            if (generatorCard != null && !tutorialGeneratorCardShown)
            {
                RevealTutorialGeneratorCard(generatorCard);
                tutorialGeneratorCardShown = true;
            }

            if (sinkCard == null || tutorialSinkCardShown)
            {
                return;
            }

            tutorialSinkRevealTween?.Kill();
            tutorialSinkCanvasGroup.alpha = 0f;
            sinkCard.localScale = Vector3.one * 0.82f;
            tutorialSinkRevealTween = DOTween.Sequence()
                .Append(sinkCard.DOScale(1f, 0.36f).SetEase(Ease.OutBack))
                .Join(DOTween.To(
                        () => tutorialSinkCanvasGroup.alpha,
                        value => tutorialSinkCanvasGroup.alpha = value,
                        1f,
                        0.22f)
                    .SetEase(Ease.OutSine))
                .SetTarget(this);
            tutorialSinkCardShown = true;
        }

        private void RevealTutorialElements(RectTransform elementsCard)
        {
            if (elementsCard == null || tutorialElementsShown)
            {
                return;
            }

            tutorialElementsRevealTween?.Kill();
            elementsCard.localScale = Vector3.one * 0.82f;
            tutorialElementsRevealTween = elementsCard.DOScale(1f, 0.36f)
                .SetEase(Ease.OutBack)
                .SetTarget(this);
            tutorialElementsShown = true;
        }

        private Transform GetTowerButtonsTransform()
        {
            return towerButtons != null ? towerButtons.transform : transform.Find("Tower Buttons");
        }


        private void RevealTutorialGeneratorCard(RectTransform generatorCard)
        {
            if (generatorCard == null)
            {
                return;
            }

            tutorialGeneratorRevealTween?.Kill();
            tutorialGeneratorCanvasGroup.alpha = 0f;
            generatorCard.localScale = Vector3.one * 0.82f;
            tutorialGeneratorRevealTween = DOTween.Sequence()
                .Append(generatorCard.DOScale(1f, 0.36f).SetEase(Ease.OutBack))
                .Join(DOTween.To(
                        () => tutorialGeneratorCanvasGroup.alpha,
                        value => tutorialGeneratorCanvasGroup.alpha = value,
                        1f,
                        0.22f)
                    .SetEase(Ease.OutSine))
                .SetTarget(this);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void Shutdown()
        {
            tutorialGeneratorRevealTween?.Kill();
            tutorialGeneratorRevealTween = null;
            waterUnlockTween?.Kill();
            waterUnlockTween = null;
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

        private void OnDisable()
        {
            tutorialGeneratorRevealTween?.Kill();
            tutorialGeneratorRevealTween = null;
            waterUnlockTween?.Kill();
            waterUnlockTween = null;
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
