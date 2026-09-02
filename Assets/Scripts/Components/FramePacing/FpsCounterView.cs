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
