using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Fires the party cannons that go off behind the victory modal.
    /// </summary>
    /// <remarks>
    /// The sheet draws one shot fired upwards - a narrow column that opens into a cone and then
    /// rains back down - so an emitter parked at the bottom edge of the screen throws its confetti
    /// up over the card without anything here having to move it.
    ///
    /// Played by flipping <see cref="Image.sprite"/> through the frames on a linear tween rather
    /// than by an Animator, which is how the rest of this HUD animates: the modal runs while the
    /// game is paused, so every tween here is on unscaled time, and the victory sequence's skip
    /// button expects to be able to call <see cref="Stop"/> and have the whole thing let go.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ConfettiBurstView : MonoBehaviour
    {
        [Tooltip("The shot's frames, in order.")]
        [SerializeField] private Sprite[] frames = Array.Empty<Sprite>();

        [Tooltip("One per cannon. Each fires the same frames from wherever it is parked.")]
        [SerializeField] private Image[] emitters = Array.Empty<Image>();

        [SerializeField, Min(1f)] private float framesPerSecond = 30f;

        [Tooltip("How long each cannon holds back behind the one before it, so the two sides read as two shots rather than one wide one.")]
        [SerializeField, Min(0f)] private float emitterStagger = 0.18f;

        private void Awake()
        {
            HideEmitters();
        }

        public void Play()
        {
            Stop();
            if (frames.Length == 0)
            {
                return;
            }

            float duration = (frames.Length - 1) / framesPerSecond;
            for (int index = 0; index < emitters.Length; index++)
            {
                Image emitter = emitters[index];
                if (emitter == null)
                {
                    continue;
                }

                emitter.sprite = frames[0];
                emitter.gameObject.SetActive(true);
                int frame = 0;
                DOTween.To(
                        () => frame,
                        value =>
                        {
                            frame = value;
                            emitter.sprite = frames[Mathf.Clamp(value, 0, frames.Length - 1)];
                        },
                        frames.Length - 1,
                        duration)
                    .SetEase(Ease.Linear)
                    .SetDelay(index * emitterStagger)
                    .SetUpdate(true)
                    .SetTarget(this)
                    .OnComplete(() => emitter.gameObject.SetActive(false));
            }
        }

        public void Stop()
        {
            DOTween.Kill(this);
            HideEmitters();
        }

        private void HideEmitters()
        {
            for (int index = 0; index < emitters.Length; index++)
            {
                if (emitters[index] != null)
                {
                    emitters[index].gameObject.SetActive(false);
                }
            }
        }

        private void OnDisable()
        {
            Stop();
        }
    }
}
