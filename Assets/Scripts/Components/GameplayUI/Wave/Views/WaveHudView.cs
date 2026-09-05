using System;
using System.Collections.Generic;
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

        public event Action StartWaveRequested;

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

        public void Shutdown()
        {
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

        private void HandleStartWaveRequested()
        {
            StartWaveRequested?.Invoke();
        }
    }
}
