using System;
using System.Collections.Generic;
using DG.Tweening;
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

        private bool isInitialized;
        private bool isPreviewExpanded;
        private bool isTutorialPreviewOnly;
        private CanvasGroup startWaveCanvasGroup;
        private Tween startWaveRevealTween;

        public event Action StartWaveRequested;
        public Transform NextWaveToggleTransform => previewToggleButton != null
            ? previewToggleButton.transform
            : null;
        public Transform NextEnemyTransform => previewSlots != null
            && previewSlots.Length > 0
            && previewSlots[0] != null
                ? previewSlots[0].transform
                : previewGrid != null ? previewGrid.transform : null;

        public void Initialize()
        {
            if (isInitialized)
            {
                return;
            }

            startWaveButton.onClick.AddListener(HandleStartWaveRequested);
            if (previewToggleButton != null)
            {
                previewToggleButton.onClick.AddListener(HandlePreviewToggled);
            }

            SetPreviewExpanded(previewStartsExpanded);
            isInitialized = true;
        }

        public void Render(WaveHudState state)
        {
            startWaveButton.interactable = state.StartWaveEnabled;
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
            RenderPreviewSlots(state.PreviewIcons);
        }

        /// <summary>
        /// Fills the portrait slots left to right and hides the rest.
        /// </summary>
        /// <remarks>
        /// Hidden rather than cleared: a slot holding a null sprite still draws its own frame, so
        /// an eight-slot grid showing three enemies would read as five enemies the game had failed
        /// to name.
        /// </remarks>
        private void RenderPreviewSlots(IReadOnlyList<Sprite> icons)
        {
            int count = icons == null ? 0 : icons.Count;
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
        }

        private void SetPreviewExpanded(bool expanded)
        {
            isPreviewExpanded = expanded;

            if (previewGrid != null)
            {
                previewGrid.SetActive(expanded);
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
            if (!isInitialized)
            {
                return;
            }

            startWaveButton.onClick.RemoveListener(HandleStartWaveRequested);
            if (previewToggleButton != null)
            {
                previewToggleButton.onClick.RemoveListener(HandlePreviewToggled);
            }

            isInitialized = false;
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
                startWaveCanvasGroup = startWaveButton.GetComponent<CanvasGroup>();
                if (startWaveCanvasGroup == null)
                {
                    startWaveCanvasGroup = startWaveButton.gameObject.AddComponent<CanvasGroup>();
                }
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
            startWaveButton.gameObject.SetActive(visible);
            startWaveButton.transform.localScale = Vector3.one;
            if (startWaveCanvasGroup != null) startWaveCanvasGroup.alpha = 1f;
        }

        private void OnDisable()
        {
            startWaveRevealTween?.Kill();
            startWaveRevealTween = null;
        }

        private void HandleStartWaveRequested()
        {
            StartWaveRequested?.Invoke();
        }
    }
}
