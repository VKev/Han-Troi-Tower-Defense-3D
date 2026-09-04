using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// The opening: black, then the splash, then the title, then the journey. Every handover is
    /// made behind a full-screen black curtain, so nothing is ever seen appearing or disappearing.
    /// </summary>
    /// <remarks>
    /// One component owns the whole opening rather than each panel dismissing itself. The panels
    /// have to be swapped while the screen is covered, which means somebody has to know both the
    /// running order and how far through the fade we are; splitting that across the panels would
    /// only mean two of them writing the same curtain.
    ///
    /// Boot runs the moment the application starts, behind the curtain, so the journey menu is
    /// already sitting underneath by the time the last fade uncovers it. If progress could not be
    /// read, what gets uncovered is the blocking error instead - which is why the curtain never
    /// hides anything permanently, it only ever fades back to nothing.
    ///
    /// The curtain does not take raycasts. Blocking input while it is up would be the obvious
    /// thing, but the tap is only listened for during the one phase that wants it, so there is
    /// nothing to block - and a full-screen raycast target on its own nested canvas is exactly
    /// what stopped the title from being tappable before.
    ///
    /// Timed on unscaled time: the opening runs before anything sets a time scale, and would
    /// otherwise stall for good behind a pause that arrived early.
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class OpeningSequenceView : MonoBehaviour
    {
        [Tooltip("Shown first, uncovered for the hold below, then swapped for the title.")]
        [SerializeField] private GameObject splash;

        [Tooltip("The title panel, uncovered after the splash and left up until it is tapped.")]
        [SerializeField] private GameObject gameStart;

        [Tooltip("Tapped to leave the title. Listened to only once the title is fully uncovered, so an early tap cannot skip the splash.")]
        [SerializeField] private Button gameStartButton;

        [Tooltip("How long one fade takes. Every fade in the opening uses this.")]
        [SerializeField, Min(0f)] private float fadeSeconds = 0.5f;

        [Tooltip("How long the splash is held once it is fully uncovered, not counting the fades either side of it.")]
        [SerializeField, Min(0f)] private float splashHoldSeconds = 2f;

        /// <summary>
        /// The most one frame may advance a fade. Anything longer is a hitch rather than a frame,
        /// and letting it through would skip the fade instead of slowing it.
        /// </summary>
        private const float MaxFadeStepSeconds = 1f / 30f;

        private Image curtain;
        private bool wasTapped;

        private void Awake()
        {
            curtain = GetComponent<Image>();

            // Opaque before the first frame is drawn, so the opening starts black rather than
            // flashing whatever the scene was authored showing.
            SetCurtainAlpha(1f);

            if (splash != null)
            {
                splash.SetActive(true);
            }

            if (gameStart != null)
            {
                gameStart.SetActive(false);
            }
        }

        private void OnEnable()
        {
            StartCoroutine(Run());
        }

        private void OnDisable()
        {
            if (gameStartButton != null)
            {
                gameStartButton.onClick.RemoveListener(HandleTapped);
            }
        }

        private IEnumerator Run()
        {
            yield return Fade(1f, 0f);
            yield return Hold(splashHoldSeconds);
            yield return Fade(0f, 1f);

            Swap(splash, gameStart);
            yield return Fade(1f, 0f);

            yield return AwaitTap();
            yield return Fade(0f, 1f);

            if (gameStart != null)
            {
                gameStart.SetActive(false);
            }

            yield return Fade(1f, 0f);
        }

        private static void Swap(GameObject hide, GameObject show)
        {
            if (hide != null)
            {
                hide.SetActive(false);
            }

            if (show != null)
            {
                show.SetActive(true);
            }
        }

        private IEnumerator AwaitTap()
        {
            if (gameStartButton == null)
            {
                Debug.LogError(
                    "OpeningSequenceView has no game start button, so the title cannot be left.",
                    this);
                yield break;
            }

            wasTapped = false;
            gameStartButton.onClick.AddListener(HandleTapped);
            while (!wasTapped)
            {
                yield return null;
            }

            gameStartButton.onClick.RemoveListener(HandleTapped);
        }

        private void HandleTapped()
        {
            wasTapped = true;
        }

        private IEnumerator Fade(float from, float to)
        {
            if (fadeSeconds <= 0f)
            {
                SetCurtainAlpha(to);
                yield break;
            }

            float elapsedSeconds = 0f;
            while (elapsedSeconds < fadeSeconds)
            {
                // Clamped, because the frame that carries the application boot - scene load,
                // container build, save read - reports several seconds of delta at once. Charging
                // that against the fade finishes it inside one frame, and the curtain is then
                // never seen lifting, it just pops. A fade that has to be watched cannot be
                // advanced faster than it can be drawn.
                elapsedSeconds += Mathf.Min(Time.unscaledDeltaTime, MaxFadeStepSeconds);
                SetCurtainAlpha(Mathf.Lerp(from, to, elapsedSeconds / fadeSeconds));
                yield return null;
            }

            // Landed exactly, rather than wherever the last frame's step happened to reach.
            SetCurtainAlpha(to);
        }

        /// <summary>
        /// Waits out a duration on accumulated unscaled time.
        /// </summary>
        /// <remarks>
        /// Not <c>WaitForSecondsRealtime</c>, which reads the absolute clock once on construction
        /// and compares against it forever after. Built on the first frame, before that clock has
        /// been rebased for the new run, it latches a target the clock then never reaches and the
        /// opening waits for hours. Counting deltas has no absolute reference to get wrong.
        ///
        /// Unclamped, unlike the fades: a hold is a duration to be spent, not a movement to be
        /// watched, so a hitch should shorten the remaining wait rather than extend it.
        /// </remarks>
        private static IEnumerator Hold(float seconds)
        {
            float elapsedSeconds = 0f;
            while (elapsedSeconds < seconds)
            {
                elapsedSeconds += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void SetCurtainAlpha(float alpha)
        {
            Color color = curtain.color;
            color.a = alpha;
            curtain.color = color;
        }
    }
}
