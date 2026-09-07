using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// A full-screen black curtain that fades up to cover the screen and back down to uncover it.
    /// It is the one thing that hides a handover: the opening's move from title to journey, and
    /// the scene swap on the way into a level.
    /// </summary>
    /// <remarks>
    /// One component owns the curtain's alpha, and everything that wants the screen covered asks
    /// it rather than fading an image of its own. Two owners would each be writing the same
    /// colour on the same frame, and the last writer would win by accident.
    ///
    /// Every fade reports back through a callback, and every path that can end one reports: a
    /// caller waiting for the cover before it loads a scene must never be left waiting, or the
    /// game sits on a black screen for good. That is why being disabled mid-fade finishes the
    /// fade instantly rather than dropping it.
    ///
    /// Timed on unscaled time, and each step clamped. The opening runs before anything has set a
    /// time scale, and the frame carrying a scene load reports several seconds of delta at once -
    /// charged against the fade, that would finish it inside one frame and the curtain would pop
    /// rather than fade.
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class ScreenFadeView : MonoBehaviour
    {
        private const float MaxStepSeconds = 1f / 30f;

        [Tooltip("How long one fade takes, covering or uncovering.")]
        [SerializeField, Min(0.01f)] private float fadeSeconds = 0.35f;

        private Image curtain;
        private Coroutine fade;
        private Action pendingCallback;
        private float pendingTarget;
        private float coverage;

        /// <summary>Whether the screen is fully covered.</summary>
        public bool IsCovered => coverage >= 1f;

        private void Awake()
        {
            curtain = GetComponent<Image>();

            // Opaque before the first frame is drawn, so a cold start begins black rather than
            // flashing whatever the scene was authored showing.
            Apply(1f);
        }

        /// <summary>Fades to black and reports once the screen is covered.</summary>
        public void Cover(Action onCovered)
        {
            FadeTo(1f, onCovered);
        }

        /// <summary>Fades back to nothing and reports once the screen is clear.</summary>
        public void Uncover(Action onUncovered)
        {
            FadeTo(0f, onUncovered);
        }

        private void FadeTo(float target, Action onFinished)
        {
            // With nothing to fade the transition still has to continue, so the caller is told
            // straight away rather than left waiting on a curtain that cannot move.
            if (!isActiveAndEnabled)
            {
                Apply(target);
                onFinished?.Invoke();
                return;
            }

            StopFade();
            pendingCallback = onFinished;
            pendingTarget = target;
            fade = StartCoroutine(FadeRoutine(target, onFinished));
        }

        private IEnumerator FadeRoutine(float target, Action onFinished)
        {
            float from = coverage;
            if (Mathf.Approximately(from, target))
            {
                Finish(target, onFinished);
                yield break;
            }

            // Scaled by the distance actually travelled, so a fade that starts halfway is not
            // given the same time as a whole one and does not crawl.
            float duration = fadeSeconds * Mathf.Abs(target - from);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Mathf.Min(Time.unscaledDeltaTime, MaxStepSeconds);
                Apply(Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, elapsed / duration)));
                yield return null;
            }

            Finish(target, onFinished);
        }

        private void Finish(float target, Action onFinished)
        {
            fade = null;
            pendingCallback = null;
            Apply(target);
            onFinished?.Invoke();
        }

        private void Apply(float amount)
        {
            coverage = Mathf.Clamp01(amount);
            if (curtain == null)
            {
                curtain = GetComponent<Image>();
            }

            Color color = curtain.color;
            color.a = coverage;
            curtain.color = color;

            // A clear curtain must not swallow taps meant for what is behind it, and an opaque one
            // has nothing behind it worth tapping - so it never takes raycasts either way. Input
            // during a transition is stopped by the flow's own blocker.
            curtain.raycastTarget = false;
        }

        private void StopFade()
        {
            if (fade != null)
            {
                StopCoroutine(fade);
                fade = null;
            }
        }

        /// <summary>
        /// Ends whatever fade is running, puts the curtain at <paramref name="target"/>, and tells
        /// the waiter. Every path that can cut a fade short goes through here: a caller left
        /// waiting on a fade that was stopped never resumes, and there is no error to see.
        /// </summary>
        private void CompletePending(float target)
        {
            StopFade();
            Action callback = pendingCallback;
            pendingCallback = null;
            Apply(target);
            callback?.Invoke();
        }

        /// <summary>
        /// A fade cut short still has to report, or a transition waiting on it never resumes. The
        /// curtain is snapped to where the fade was heading and the caller told, so a level load
        /// waiting on the cover carries on instead of sitting behind a half-drawn curtain.
        /// </summary>
        private void OnDisable()
        {
            if (fade == null && pendingCallback == null)
            {
                return;
            }

            CompletePending(pendingTarget);
        }
    }
}
