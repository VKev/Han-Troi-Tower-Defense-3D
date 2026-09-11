using System;
using System.Collections.Generic;
using DG.Tweening;
using TowerDefense3D.Enemies;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class WaveHudView : MonoBehaviour, IWaveHudView
    {
        [SerializeField] private Button startWaveButton;
        [SerializeField] private Text startWaveText;
        [SerializeField] private Text startWaveBonusText;
        [SerializeField] private Text waveCounterText;

        [Tooltip("Optional. The plaque carries the wave numbers and nothing else, so there is nowhere for a status line; wire a Text here and it comes back.")]
        [SerializeField] private Text statusText;

        [Tooltip("Optional. Same story as the status line: the plaque has no progress bar. Wire an Image here and it fills again.")]
        [SerializeField] private Image waveProgressFill;

        [SerializeField] private Text enemiesLeftText;

        [Header("Next wave preview")]
        [Tooltip("The NEXT WAVE plaque. Tapping it rolls the portrait grid out and back in.")]
        [SerializeField] private Button previewToggleButton;

        [Tooltip("Everything that rolls out: the portrait grid and whatever backs it.")]
        [SerializeField] private GameObject previewGrid;

        [Tooltip("The chevron on the plaque. Flipped vertically while the grid is out, so it points back the way the grid will go.")]
        [SerializeField] private RectTransform previewChevron;

        [Tooltip("Portrait slots, filled left to right. Spare slots are hidden rather than left blank.")]
        [SerializeField] private Image[] previewSlots = Array.Empty<Image>();

        [Tooltip("Whether the grid starts rolled out. It does: the roster is what the player is deciding against, so it is the resting state rather than something to go looking for. After that it is theirs - nothing closes it but another tap.")]
        [SerializeField] private bool previewStartsExpanded = true;
        [SerializeField] private GameObject enemyDescriptionPanel;
        [SerializeField] private Text enemyDescriptionText;
        [SerializeField] private CanvasGroup startWaveCanvasGroup;

        [Header("Chain hint")]
        [Tooltip("The red line above the build bar. It stands there for as long as no chain feeds a Soul Nexus, and goes away the moment one does.")]
        [SerializeField] private Text startWaveBlockedHintText;

        [Tooltip("Sits on the Start Wave button and reports the taps the button drops while it is greyed out.")]
        [SerializeField] private StartWavePressRelay startWavePressRelay;

        [SerializeField, Min(0f)] private float blockedHintPunchDuration = 0.45f;
        [SerializeField, Min(0f)] private float blockedHintPunchScale = 0.22f;

        private bool isInitialized;
        private bool isPreviewExpanded;
        private bool isTutorialPreviewOnly;
        private Tween startWaveRevealTween;
        private Tween startWavePressTween;
        private Tween blockedHintTween;
        private Tween previewGridTween;
        private Tween enemyDescriptionTween;
        private Tween[] previewAttentionTweens = Array.Empty<Tween>();
        private EnemyDefinition[] previewEnemies = Array.Empty<EnemyDefinition>();
        private int describedEnemyIndex = -1;

        public event Action StartWaveRequested;
        public event Action<EnemyDefinition> EnemyDescriptionOpened;
        public Transform NextWaveToggleTransform => previewToggleButton != null
            ? previewToggleButton.transform
            : null;
        public Transform NextEnemyTransform => previewSlots != null
            && previewSlots.Length > 0
            && previewSlots[0] != null
                ? previewSlots[0].transform
                : previewGrid != null ? previewGrid.transform : null;
        public Transform NextEnemyDescriptionTransform => enemyDescriptionPanel != null
            ? enemyDescriptionPanel.transform
            : null;
        public bool IsNextEnemyDescriptionVisible => describedEnemyIndex == 0
            && enemyDescriptionPanel != null
            && enemyDescriptionPanel.activeSelf;

        public void Initialize()
        {
            if (isInitialized)
            {
                return;
            }

            if (startWaveCanvasGroup == null
                || startWaveCanvasGroup.gameObject != startWaveButton.gameObject)
            {
                throw new MissingReferenceException(
                    "WaveHudView requires Start Wave to own its authored CanvasGroup.");
            }

            startWaveButton.onClick.AddListener(HandleStartWaveRequested);
            if (startWavePressRelay != null)
            {
                startWavePressRelay.Pressed += HandleStartWavePressed;
            }

            if (previewToggleButton != null)
            {
                previewToggleButton.onClick.AddListener(HandlePreviewToggled);
            }

            for (int index = 0; index < previewSlots.Length; index++)
            {
                int slotIndex = index;
                Image slot = previewSlots[index];
                if (slot == null)
                {
                    continue;
                }

                Button button = slot.GetComponent<Button>();
                if (button == null)
                {
                    throw new InvalidOperationException($"Preview slot {index + 1} requires an authored Button.");
                }
                button.targetGraphic = slot;
                slot.raycastTarget = true;
                button.interactable = true;
                button.onClick.AddListener(() => ToggleEnemyDescription(slotIndex));
            }

            SetPreviewExpanded(previewStartsExpanded);
            isInitialized = true;
        }

        public void Render(WaveHudState state)
        {
            startWaveButton.interactable = state.StartWaveEnabled;
            if (isTutorialPreviewOnly && !state.StartWaveEnabled)
            {
                startWaveButton.gameObject.SetActive(false);
            }

            // Gated on the button still being on screen as well. A tutorial step that takes Start
            // Wave away has its own instruction running, and a line explaining how to un-grey a
            // button the player cannot see would only argue with it.
            RenderBlockedHint(
                state.ShowStartWaveBlockedHint && startWaveButton.gameObject.activeSelf);
            startWaveText.text = state.StartWaveText;
            startWaveBonusText.text = state.StartWaveBonusText;
            waveCounterText.text = state.WaveCounterText;

            if (statusText != null)
            {
                statusText.text = state.StatusText;
            }

            if (waveProgressFill != null)
            {
                waveProgressFill.fillAmount = Mathf.Clamp01(state.WaveProgress);
            }

            enemiesLeftText.text = state.EnemiesLeftText;
            RenderPreviewSlots(
                state.PreviewIcons,
                state.PreviewEnemies,
                state.PreviewEnemiesAreNew);
        }

        /// <summary>
        /// Fills the portrait slots left to right and hides the rest.
        /// </summary>
        /// <remarks>
        /// Hidden rather than cleared: a slot holding a null sprite still draws its own frame, so
        /// an eight-slot grid showing three enemies would read as five enemies the game had failed
        /// to name.
        /// </remarks>
        private void RenderPreviewSlots(
            IReadOnlyList<Sprite> icons,
            IReadOnlyList<EnemyDefinition> enemies,
            IReadOnlyList<bool> enemiesAreNew)
        {
            int count = icons == null ? 0 : icons.Count;
            EnemyDefinition[] nextPreviewEnemies = enemies == null
                ? Array.Empty<EnemyDefinition>()
                : ToArray(enemies);
            if (!HasSamePreviewEnemies(nextPreviewEnemies))
            {
                HideEnemyDescription();
            }

            previewEnemies = nextPreviewEnemies;
            EnsurePreviewAttentionCapacity();
            for (int index = 0; index < previewSlots.Length; index++)
            {
                Image slot = previewSlots[index];
                if (slot == null)
                {
                    continue;
                }

                bool filled = index < count;
                slot.sprite = filled ? icons[index] : null;
                slot.enabled = filled;
                bool shouldAnimate = filled
                    && enemiesAreNew != null
                    && index < enemiesAreNew.Count
                    && enemiesAreNew[index];
                SetPreviewAttention(index, shouldAnimate);
            }

            if (describedEnemyIndex >= previewEnemies.Length)
            {
                HideEnemyDescription();
            }
        }

        /// <summary>
        /// The grid is the player's: a tap opens it, another shuts it, in any phase.
        /// </summary>
        /// <remarks>
        /// It used to slam shut and stop answering taps the moment a wave started. That took the
        /// control away exactly when a player might want to check what is still coming, so the
        /// phase no longer has a say in it.
        /// </remarks>
        private void HandlePreviewToggled()
        {
            SetPreviewExpanded(!isPreviewExpanded);
            if (!isPreviewExpanded)
            {
                HideEnemyDescription();
            }
        }

        private void ToggleEnemyDescription(int index)
        {
            if (index < 0 || index >= previewEnemies.Length || previewEnemies[index] == null)
            {
                return;
            }

            if (describedEnemyIndex == index && enemyDescriptionPanel != null && enemyDescriptionPanel.activeSelf)
            {
                HideEnemyDescription();
                return;
            }

            if (!EnsureEnemyDescriptionPanel())
            {
                return;
            }

            describedEnemyIndex = index;
            StopPreviewAttention(index);
            EnemyDescriptionOpened?.Invoke(previewEnemies[index]);
            enemyDescriptionText.text = previewEnemies[index].Description;
            RectTransform panel = enemyDescriptionPanel.transform as RectTransform;
            RectTransform slot = previewSlots[index].rectTransform;
            float panelHeight = Mathf.Max(
                112f,
                LayoutUtility.GetPreferredHeight(enemyDescriptionText.rectTransform) + 36f);
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, panelHeight);
            float gap = 12f;
            float offset = (slot.rect.width + panel.rect.width) * 0.5f + gap;
            RectTransform parent = panel.parent as RectTransform;
            if (parent == null)
            {
                return;
            }

            Vector2 slotPosition = parent.InverseTransformPoint(slot.position);
            float x = slotPosition.x + offset;
            if (x + panel.rect.width * 0.5f > parent.rect.xMax)
            {
                x = slotPosition.x - offset;
            }

            float y = Mathf.Clamp(
                slotPosition.y,
                parent.rect.yMin + panel.rect.height * 0.5f,
                parent.rect.yMax - panel.rect.height * 0.5f);
            panel.anchoredPosition = new Vector2(x, y);
            ShowEnemyDescription(panel);
        }

        public bool ShowNextEnemyDescription()
        {
            if (!IsNextEnemyDescriptionVisible)
            {
                ToggleEnemyDescription(0);
            }

            return IsNextEnemyDescriptionVisible;
        }

        private void HideEnemyDescription()
        {
            describedEnemyIndex = -1;
            if (enemyDescriptionPanel == null)
            {
                return;
            }

            enemyDescriptionTween?.Kill();
            enemyDescriptionTween = null;
            if (!enemyDescriptionPanel.activeSelf)
            {
                return;
            }

            RectTransform panel = enemyDescriptionPanel.transform as RectTransform;
            enemyDescriptionTween = DOTween.Sequence()
                .Join(panel.DOScale(0.92f, 0.16f).SetEase(Ease.InSine))
                .OnComplete(() => enemyDescriptionPanel.SetActive(false))
                .SetUpdate(true)
                .SetTarget(this);
        }

        private bool EnsureEnemyDescriptionPanel()
        {
            return enemyDescriptionPanel != null && enemyDescriptionText != null;
        }

        private void ShowEnemyDescription(RectTransform panel)
        {
            enemyDescriptionTween?.Kill();
            enemyDescriptionTween = null;
            enemyDescriptionPanel.SetActive(true);
            panel.localScale = Vector3.one * 0.92f;
            enemyDescriptionTween = DOTween.Sequence()
                .Join(panel.DOScale(1f, 0.24f).SetEase(Ease.OutBack))
                .SetUpdate(true)
                .SetTarget(this);
        }

        private static EnemyDefinition[] ToArray(IReadOnlyList<EnemyDefinition> source)
        {
            var result = new EnemyDefinition[source.Count];
            for (int index = 0; index < source.Count; index++) result[index] = source[index];
            return result;
        }

        private void EnsurePreviewAttentionCapacity()
        {
            if (previewAttentionTweens.Length == previewSlots.Length)
            {
                return;
            }

            for (int index = 0; index < previewAttentionTweens.Length; index++)
            {
                previewAttentionTweens[index]?.Kill();
            }

            previewAttentionTweens = new Tween[previewSlots.Length];
        }

        private void SetPreviewAttention(int index, bool enabled)
        {
            if (!enabled)
            {
                StopPreviewAttention(index);
                return;
            }

            if (previewAttentionTweens[index] != null && previewAttentionTweens[index].active)
            {
                return;
            }

            RectTransform slot = previewSlots[index].rectTransform;
            slot.localScale = Vector3.one;
            previewAttentionTweens[index] = slot
                .DOScale(1.11f, 0.48f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true)
                .SetTarget(this);
        }

        private void StopPreviewAttention(int index)
        {
            if (index < 0 || index >= previewAttentionTweens.Length)
            {
                return;
            }

            previewAttentionTweens[index]?.Kill();
            previewAttentionTweens[index] = null;
            if (index < previewSlots.Length && previewSlots[index] != null)
            {
                previewSlots[index].rectTransform.localScale = Vector3.one;
            }
        }

        private bool HasSamePreviewEnemies(IReadOnlyList<EnemyDefinition> nextPreviewEnemies)
        {
            if (previewEnemies.Length != nextPreviewEnemies.Count)
            {
                return false;
            }

            for (int index = 0; index < previewEnemies.Length; index++)
            {
                if (previewEnemies[index] != nextPreviewEnemies[index])
                {
                    return false;
                }
            }

            return true;
        }

        private void SetPreviewExpanded(bool expanded)
        {
            bool changed = isPreviewExpanded != expanded;
            isPreviewExpanded = expanded;

            if (previewGrid != null)
            {
                AnimatePreviewGrid(expanded, changed);
            }

            if (previewChevron != null)
            {
                // Flipped rather than rotated: the chevron art is symmetrical left to right, so
                // mirroring it costs one scale write and cannot drift out of alignment the way a
                // 180 degree rotation around a pivot that is not dead centre would.
                Vector3 scale = previewChevron.localScale;
                scale.y = expanded ? -Mathf.Abs(scale.y) : Mathf.Abs(scale.y);
                previewChevron.localScale = scale;
            }
        }

        private void AnimatePreviewGrid(bool expanded, bool changed)
        {
            previewGridTween?.Kill();
            previewGridTween = null;
            if (!changed)
            {
                previewGrid.SetActive(expanded);
                return;
            }

            RectTransform rect = previewGrid.transform as RectTransform;
            if (expanded)
            {
                previewGrid.SetActive(true);
                rect.localScale = Vector3.one * 0.92f;
                previewGridTween = DOTween.Sequence()
                    .Join(rect.DOScale(1f, 0.24f).SetEase(Ease.OutBack))
                    .SetUpdate(true)
                    .SetTarget(this);
                return;
            }

            previewGridTween = DOTween.Sequence()
                .Join(rect.DOScale(0.92f, 0.16f).SetEase(Ease.InSine))
                .OnComplete(() => previewGrid.SetActive(false))
                .SetUpdate(true)
                .SetTarget(this);
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void SetTutorialPreviewOnly(bool enabled)
        {
            isTutorialPreviewOnly = enabled;
            if (enabled)
            {
                SetTutorialWaveVisibility(false, true);
                return;
            }

            ResetStartWaveReveal(true);
            if (waveCounterText != null) waveCounterText.gameObject.SetActive(true);
            if (enemiesLeftText != null) enemiesLeftText.gameObject.SetActive(true);
            if (statusText != null) statusText.gameObject.SetActive(true);
            if (waveProgressFill != null) waveProgressFill.gameObject.SetActive(true);
            if (startWaveBonusText != null) startWaveBonusText.gameObject.SetActive(true);
            if (previewToggleButton != null) previewToggleButton.gameObject.SetActive(true);
            if (previewGrid != null) previewGrid.SetActive(true);
        }

        public void SetTutorialStartWaveOnly()
        {
            isTutorialPreviewOnly = true;
            SetTutorialWaveVisibility(true, true);
        }

        private void SetTutorialWaveVisibility(bool showStartWave, bool showCounters)
        {
            if (showStartWave) RevealStartWave(); else ResetStartWaveReveal(false);
            if (waveCounterText != null) waveCounterText.gameObject.SetActive(showCounters);
            if (enemiesLeftText != null) enemiesLeftText.gameObject.SetActive(showCounters);
            if (statusText != null) statusText.gameObject.SetActive(false);
            if (waveProgressFill != null) waveProgressFill.gameObject.SetActive(false);
            if (startWaveBonusText != null) startWaveBonusText.gameObject.SetActive(false);
            if (previewToggleButton != null) previewToggleButton.gameObject.SetActive(true);
            SetPreviewExpanded(true);
        }

        public void Shutdown()
        {
            startWaveRevealTween?.Kill();
            startWaveRevealTween = null;
            startWavePressTween?.Kill();
            blockedHintTween?.Kill();
            previewGridTween?.Kill();
            enemyDescriptionTween?.Kill();
            for (int index = 0; index < previewAttentionTweens.Length; index++)
            {
                previewAttentionTweens[index]?.Kill();
            }
            if (!isInitialized)
            {
                return;
            }

            startWaveButton.onClick.RemoveListener(HandleStartWaveRequested);
            if (startWavePressRelay != null)
            {
                startWavePressRelay.Pressed -= HandleStartWavePressed;
            }

            if (previewToggleButton != null)
            {
                previewToggleButton.onClick.RemoveListener(HandlePreviewToggled);
            }

            isInitialized = false;
        }

        private void RenderBlockedHint(bool visible)
        {
            if (startWaveBlockedHintText == null
                || startWaveBlockedHintText.gameObject.activeSelf == visible)
            {
                return;
            }

            // Any punch still running belongs to a question the player has just had answered, so
            // it is dropped rather than left to finish on a line that is on its way out.
            blockedHintTween?.Kill();
            startWaveBlockedHintText.gameObject.SetActive(visible);
        }

        /// <summary>
        /// A refused tap on Start Wave nudges the line that explains the refusal, then leaves it
        /// static again.
        /// </summary>
        private void HandleStartWavePressed()
        {
            if (startWaveButton.interactable
                || startWaveBlockedHintText == null
                || !startWaveBlockedHintText.gameObject.activeInHierarchy
                || blockedHintPunchDuration <= 0f
                || blockedHintPunchScale <= 0f)
            {
                return;
            }

            // Restarted rather than stacked, and the scale is put back by hand first: a punch
            // reads its rest pose when it starts, so punching mid-punch would settle the line on
            // whatever half-scaled value the previous one happened to be passing through.
            blockedHintTween?.Kill();
            RectTransform hint = startWaveBlockedHintText.rectTransform;
            hint.localScale = Vector3.one;
            blockedHintTween = hint
                .DOPunchScale(
                    Vector3.one * blockedHintPunchScale,
                    blockedHintPunchDuration,
                    8,
                    0.7f)
                .SetTarget(this)
                .OnKill(ResetBlockedHintScale);
        }

        private void ResetBlockedHintScale()
        {
            blockedHintTween = null;
            if (startWaveBlockedHintText != null)
            {
                startWaveBlockedHintText.rectTransform.localScale = Vector3.one;
            }
        }

        private void RevealStartWave()
        {
            ResetStartWaveReveal(true);
            RectTransform rect = startWaveButton.transform as RectTransform;
            if (rect == null)
            {
                return;
            }

            if (startWaveCanvasGroup == null)
            {
                throw new InvalidOperationException("Start Wave requires an authored CanvasGroup.");
            }
            startWaveCanvasGroup.alpha = 0f;
            rect.localScale = Vector3.one * 0.82f;
            startWaveRevealTween = DOTween.Sequence()
                .Append(rect.DOScale(1f, 0.38f).SetEase(Ease.OutBack))
                .Join(DOTween.To(
                        () => startWaveCanvasGroup.alpha,
                        value => startWaveCanvasGroup.alpha = value,
                        1f,
                        0.22f)
                    .SetEase(Ease.OutSine))
                .SetTarget(this);
        }

        private void ResetStartWaveReveal(bool visible)
        {
            startWaveRevealTween?.Kill();
            startWaveRevealTween = null;
            startWavePressTween?.Kill();
            startWaveButton.gameObject.SetActive(visible);
            startWaveButton.transform.localScale = Vector3.one;
            if (startWaveCanvasGroup != null) startWaveCanvasGroup.alpha = 1f;
        }

        private void OnDisable()
        {
            startWaveRevealTween?.Kill();
            startWaveRevealTween = null;
            startWavePressTween?.Kill();
            previewGridTween?.Kill();
            enemyDescriptionTween?.Kill();
            for (int index = 0; index < previewAttentionTweens.Length; index++)
            {
                previewAttentionTweens[index]?.Kill();
            }
        }

        private void HandleStartWaveRequested()
        {
            startWavePressTween?.Kill();
            startWavePressTween = startWaveButton.transform
                .DOPunchScale(Vector3.one * 0.08f, 0.2f, 1, 0.5f)
                .SetUpdate(true)
                .SetTarget(this);
            StartWaveRequested?.Invoke();
        }
    }
}
