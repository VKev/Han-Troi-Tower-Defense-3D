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
            if (label != null)
            {
                label.text = "-- FPS";
            }
        }

        private void Update()
        {
            if (label == null)
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
            if (string.IsNullOrWhiteSpace(value) || label == null)
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
