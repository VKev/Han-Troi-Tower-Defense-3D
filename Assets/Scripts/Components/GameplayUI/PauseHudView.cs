using System;
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

        private bool isInitialized;

        public event Action PauseToggleRequested;

        public void Initialize()
        {
            if (isInitialized)
            {
                return;
            }

            pauseButton.onClick.AddListener(HandlePauseClicked);
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

            pauseButton.onClick.RemoveListener(HandlePauseClicked);
            isInitialized = false;
        }

        private void HandlePauseClicked()
        {
            PauseToggleRequested?.Invoke();
        }
    }
}
