using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.Mobile
{
    /// <summary>
    /// The on-screen frame rate readout.
    ///
    /// It lives on the application canvas, which Bootstrap owns and never tears down, so it carries
    /// through the level menu and on into a level without anything having to hand it over between
    /// scenes. That canvas also sorts above the gameplay HUD, so the readout stays on top of it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FpsCounterView : MonoBehaviour
    {
        [Tooltip("Whether the readout is drawn at all. Off by default: the frame rate and the graphics diagnostic beside it are profiling aids, not part of the game, so they are opted into for a session rather than shipped on.")]
        [SerializeField] private bool showOverlay;

        [SerializeField] private Text label;
        private Text diagnosticLabel;

        [Tooltip("How long each average is measured over. Longer is steadier but slower to react.")]
        [SerializeField] private float sampleWindowSeconds = 0.25f;

        [SerializeField] private Color goodColor = new(0.62f, 0.95f, 0.60f, 1f);
        [SerializeField] private Color fairColor = new(1f, 0.83f, 0.34f, 1f);
        [SerializeField] private Color poorColor = new(1f, 0.45f, 0.40f, 1f);

        private FrameRateSampler sampler;

        private void OnEnable()
        {
            sampler = new FrameRateSampler(sampleWindowSeconds);
            ApplyOverlayVisibility();
            if (label != null)
            {
                label.text = "-- FPS";
            }
        }

        /// <summary>
        /// Hides the two labels by disabling the graphics rather than the GameObjects, so this
        /// component keeps ticking and the readout can be switched back on from the inspector
        /// mid-session without anything having to be re-created.
        /// </summary>
        private void ApplyOverlayVisibility()
        {
            if (label != null)
            {
                label.enabled = showOverlay;
            }

            if (diagnosticLabel != null)
            {
                diagnosticLabel.enabled = showOverlay;
            }
        }

        private void Update()
        {
            if (label == null || !showOverlay)
            {
                return;
            }

            // Unscaled, because a paused game still renders and how fast it renders is exactly what
            // this is for. Scaled time would read as zero the moment the game is paused.
            sampler.Add(Time.unscaledDeltaTime);
            if (!sampler.TryTakeAverage(out float framesPerSecond))
            {
                return;
            }

            label.text = Mathf.RoundToInt(framesPerSecond) + " FPS";
            label.color = ResolveColor(
                FrameRateHealthScale.Resolve(framesPerSecond, FramePacingSystem.TargetFrameRate));
        }

        public void SetDiagnostic(string value)
        {
            // Refused outright while the overlay is off, so the label is never even built: the
            // diagnostic is parented next to the frame rate rather than under it, and a hidden
            // parent would not have covered for it.
            if (!showOverlay || string.IsNullOrWhiteSpace(value) || label == null)
            {
                return;
            }

            if (diagnosticLabel == null)
            {
                var diagnosticObject = new GameObject(
                    "Graphics Diagnostic",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Text));
                diagnosticObject.transform.SetParent(label.transform.parent, false);
                diagnosticLabel = diagnosticObject.GetComponent<Text>();
                diagnosticLabel.font = label.font;
                diagnosticLabel.fontSize = 13;
                diagnosticLabel.fontStyle = FontStyle.Bold;
                diagnosticLabel.alignment = TextAnchor.MiddleLeft;
                diagnosticLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
                diagnosticLabel.verticalOverflow = VerticalWrapMode.Overflow;
                diagnosticLabel.raycastTarget = false;
                diagnosticLabel.color = new Color(1f, 0.85f, 0.34f, 1f);

                RectTransform rect = diagnosticLabel.rectTransform;
                RectTransform fpsRect = label.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = Vector2.zero;
                rect.anchoredPosition = fpsRect.anchoredPosition + new Vector2(85f, 0f);
                rect.sizeDelta = new Vector2(620f, 58f);
            }

            diagnosticLabel.text = value;
        }

        private Color ResolveColor(FrameRateHealth health)
        {
            switch (health)
            {
                case FrameRateHealth.Good:
                    return goodColor;
                case FrameRateHealth.Fair:
                    return fairColor;
                default:
                    return poorColor;
            }
        }
    }
}
