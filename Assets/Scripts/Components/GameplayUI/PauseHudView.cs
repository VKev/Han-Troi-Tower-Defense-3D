using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class PauseHudView : MonoBehaviour, IPauseHudView
    {
        [SerializeField] private Button pauseButton;

        [Tooltip("The two bars, shown while the game is running. Drawn as art rather than typed as a glyph so the button matches the rest of the plaque set at any size.")]
        [SerializeField] private GameObject pauseGlyph;

        [Tooltip("The play triangle, shown while the game is paused.")]
        [SerializeField] private GameObject playGlyph;

        [Header("Double speed")]
        [Tooltip("The button beside Pause. Same plaque, so the two read as one set of transport controls.")]
        [SerializeField] private Button fastForwardButton;

        [Tooltip("The single play triangle, shown while the game runs at normal speed.")]
        [SerializeField] private GameObject singleSpeedGlyph;

        [Tooltip("The pair of offset play triangles, shown while the game runs at double speed.")]
        [SerializeField] private GameObject doubleSpeedGlyph;

        [Tooltip("The plaque graphic tinted while double speed is on.")]
        [SerializeField] private Graphic fastForwardBackground;

        [Tooltip("The plaque tint while double speed is engaged. Dimmed, so the bright state is the one the player is used to.")]
        [SerializeField] private Color fastForwardEngagedTint = new Color(0.55f, 0.55f, 0.55f, 1f);

        [SerializeField, Min(0f)] private float fastForwardPunchDuration = 0.32f;
        [SerializeField, Min(0f)] private float fastForwardPunchScale = 0.22f;

        private bool isInitialized;
        private Color fastForwardBaseTint = Color.white;
        private bool hasCapturedFastForwardTint;
        private Tween fastForwardPunchTween;

        public event Action PauseToggleRequested;
        public event Action FastForwardToggleRequested;

        public void Initialize()
        {
            if (isInitialized)
            {
                return;
            }

            pauseButton.onClick.AddListener(HandlePauseClicked);
            if (fastForwardButton != null)
            {
                fastForwardButton.onClick.AddListener(HandleFastForwardClicked);
            }

            CaptureFastForwardTint();
            isInitialized = true;
        }

        public void Render(bool isPaused)
        {
            // The button shows what a tap will do, so a paused game shows the play triangle.
            if (pauseGlyph != null)
            {
                pauseGlyph.SetActive(!isPaused);
            }

            if (playGlyph != null)
            {
                playGlyph.SetActive(isPaused);
            }
        }

        /// <summary>
        /// One triangle at normal speed, two offset triangles at double, and the plaque dimmed
        /// while double is engaged.
        /// </summary>
        /// <remarks>
        /// Dimmed rather than lit for the engaged state, which is the way round the design asked
        /// for: normal speed is where the player spends most of the level, so that is the state
        /// the button wears its ordinary colour in.
        /// </remarks>
        public void RenderFastForward(bool isFastForward)
        {
            if (singleSpeedGlyph != null)
            {
                singleSpeedGlyph.SetActive(!isFastForward);
            }

            if (doubleSpeedGlyph != null)
            {
                doubleSpeedGlyph.SetActive(isFastForward);
            }

            CaptureFastForwardTint();
            if (fastForwardBackground != null)
            {
                fastForwardBackground.color = isFastForward
                    ? fastForwardEngagedTint
                    : fastForwardBaseTint;
            }
        }

        /// <summary>
        /// Remembers the authored plaque colour once, before anything has tinted it.
        /// </summary>
        private void CaptureFastForwardTint()
        {
            if (hasCapturedFastForwardTint || fastForwardBackground == null)
            {
                return;
            }

            fastForwardBaseTint = fastForwardBackground.color;
            hasCapturedFastForwardTint = true;
        }

        public void Show()
        {
            gameObject.SetActive(true);
            SetFastForwardButtonVisible(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            SetFastForwardButtonVisible(false);
        }

        /// <summary>
        /// Carries the double-speed button with Pause.
        /// </summary>
        /// <remarks>
        /// Toggled by hand because the button is a sibling object rather than a child: the two
        /// plaques sit side by side under the safe area, so hiding this view's own GameObject
        /// leaves the speed button standing on an otherwise cleared screen.
        /// </remarks>
        private void SetFastForwardButtonVisible(bool visible)
        {
            if (fastForwardButton != null)
            {
                fastForwardButton.gameObject.SetActive(visible);
            }
        }

        public void Shutdown()
        {
            if (!isInitialized)
            {
                return;
            }

            pauseButton.onClick.RemoveListener(HandlePauseClicked);
            if (fastForwardButton != null)
            {
                fastForwardButton.onClick.RemoveListener(HandleFastForwardClicked);
            }

            fastForwardPunchTween?.Kill();
            fastForwardPunchTween = null;
            isInitialized = false;
        }

        private void HandlePauseClicked()
        {
            PauseToggleRequested?.Invoke();
        }

        private void HandleFastForwardClicked()
        {
            PlayFastForwardPunch();
            FastForwardToggleRequested?.Invoke();
        }

        private void PlayFastForwardPunch()
        {
            if (fastForwardButton == null
                || fastForwardPunchDuration <= 0f
                || fastForwardPunchScale <= 0f)
            {
                return;
            }

            // Restarted rather than stacked, and the scale put back by hand first: a punch reads
            // its rest pose when it starts, so punching mid-punch would settle the plaque on
            // whatever half-swollen value the previous one was passing through.
            fastForwardPunchTween?.Kill();
            RectTransform rect = fastForwardButton.transform as RectTransform;
            if (rect == null)
            {
                return;
            }

            rect.localScale = Vector3.one;
            fastForwardPunchTween = rect
                .DOPunchScale(
                    Vector3.one * fastForwardPunchScale,
                    fastForwardPunchDuration,
                    7,
                    0.7f)
                .SetUpdate(true)
                .SetTarget(this)
                .OnKill(RestoreFastForwardScale);
        }

        private void RestoreFastForwardScale()
        {
            fastForwardPunchTween = null;
            if (fastForwardButton != null && fastForwardButton.transform is RectTransform rect)
            {
                rect.localScale = Vector3.one;
            }
        }

        private void OnDisable()
        {
            fastForwardPunchTween?.Kill();
            fastForwardPunchTween = null;
        }
    }
}
