using System;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class LevelSkipCheatView : MonoBehaviour, ILevelSkipCheatView
    {
        [Tooltip("Skips every remaining wave and declares the level won.")]
        [SerializeField] private Button skipButton;

        [Tooltip("Skips only the current wave. Invisible like the other one - it is a development shortcut, not a control the player is offered.")]
        [SerializeField] private Button skipWaveButton;

        [Tooltip("Whether the two skip shortcuts are offered at all. Off: they are development tools, and the corner they sit in is now shared with controls the player is meant to use. Tick it to get them back for a debugging session.")]
        [SerializeField] private bool cheatsEnabled;

        private bool isInitialized;

        public event Action SkipToVictoryRequested;

        public event Action SkipWaveRequested;

        public void Initialize()
        {
            if (isInitialized)
            {
                return;
            }

            skipButton.onClick.AddListener(HandleSkipClicked);
            if (skipWaveButton != null)
            {
                skipWaveButton.onClick.AddListener(HandleSkipWaveClicked);
            }

            ApplyCheatVisibility();
            isInitialized = true;
        }

        public void Render(bool canSkip)
        {
            // Re-asserted on every refresh, not just at startup. The tutorial stage shows and
            // hides this view's GameObject directly for its own reasons, so a one-time switch-off
            // would be undone the next time a tutorial beat wanted the corner back.
            ApplyCheatVisibility();
            skipButton.interactable = canSkip && cheatsEnabled;
            if (skipWaveButton != null)
            {
                skipWaveButton.interactable = canSkip && cheatsEnabled;
            }
        }

        public void Show()
        {
            gameObject.SetActive(cheatsEnabled);
            ApplyCheatVisibility();
        }

        /// <summary>
        /// Turns the two shortcuts off at their own GameObjects, so nothing that activates this
        /// view's parent can hand them back to the player.
        /// </summary>
        /// <remarks>
        /// Done per button rather than on the view root because the root is not this view's to
        /// keep: the tutorial stage owns it, and it activates it whenever a beat wants the corner
        /// visible. Switching the buttons themselves survives that.
        /// </remarks>
        private void ApplyCheatVisibility()
        {
            if (skipButton != null)
            {
                skipButton.gameObject.SetActive(cheatsEnabled);
            }

            if (skipWaveButton != null)
            {
                skipWaveButton.gameObject.SetActive(cheatsEnabled);
            }
        }

        public void Shutdown()
        {
            if (!isInitialized)
            {
                return;
            }

            skipButton.onClick.RemoveListener(HandleSkipClicked);
            if (skipWaveButton != null)
            {
                skipWaveButton.onClick.RemoveListener(HandleSkipWaveClicked);
            }

            isInitialized = false;
        }

        private void HandleSkipClicked()
        {
            SkipToVictoryRequested?.Invoke();
        }

        private void HandleSkipWaveClicked()
        {
            SkipWaveRequested?.Invoke();
        }
    }
}
