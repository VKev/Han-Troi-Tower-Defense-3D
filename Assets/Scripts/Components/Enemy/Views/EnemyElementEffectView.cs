using TowerDefense3D.Towers;
using UnityEngine;

namespace TowerDefense3D.Enemies
{
    /// <summary>
    /// Toggles authored Element-effect roots and animates their local scale. Each root can own
    /// any number of ParticleSystems and controls which of them play when the root is enabled.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyElementEffectView : MonoBehaviour
    {
        private sealed class EffectRoot
        {
            public EffectRoot(GameObject root)
            {
                Root = root;
                AuthoredScale = root.transform.localScale;
                Particles = root.GetComponentsInChildren<ParticleSystem>(true);
            }

            public GameObject Root { get; }
            public Vector3 AuthoredScale { get; }
            public ParticleSystem[] Particles { get; }
        }

        [SerializeField] private GameObject fireEffect;
        [SerializeField] private GameObject waterEffect;
        [SerializeField] private GameObject windEffect;
        [SerializeField] private GameObject earthEffect;
        [SerializeField, Min(0.01f)] private float transitionDurationSeconds = 0.2f;

        private EffectRoot[] effectsByElement;
        private EffectRoot activeEffect;
        private EffectRoot desiredEffect;
        private float scaleProgress;

        public void Bind(EnemyElementState state)
        {
            EnsureInitialized();
            ResetEffects();
            desiredEffect = GetDesiredEffect(state);
            ShowDesiredEffect();
        }

        public void Render(EnemyElementState state, float deltaTime)
        {
            EnsureInitialized();
            desiredEffect = GetDesiredEffect(state);
            ShowDesiredEffect();
            if (activeEffect == null)
            {
                return;
            }

            float targetProgress = activeEffect == desiredEffect ? 1f : 0f;
            scaleProgress = Mathf.MoveTowards(
                scaleProgress,
                targetProgress,
                deltaTime / transitionDurationSeconds);
            activeEffect.Root.transform.localScale = activeEffect.AuthoredScale * scaleProgress;
            if (scaleProgress > 0f || targetProgress > 0f)
            {
                return;
            }

            Deactivate(activeEffect);
            activeEffect = null;
            ShowDesiredEffect();
        }

        public void Release()
        {
            EnsureInitialized();
            ResetEffects();
        }

        private void EnsureInitialized()
        {
            if (effectsByElement != null)
            {
                return;
            }

            effectsByElement = new[]
            {
                CreateEffectRoot(fireEffect),
                CreateEffectRoot(waterEffect),
                CreateEffectRoot(windEffect),
                CreateEffectRoot(earthEffect)
            };
            ResetEffects();
        }

        private static EffectRoot CreateEffectRoot(GameObject root)
        {
            return root != null ? new EffectRoot(root) : null;
        }

        private EffectRoot GetDesiredEffect(EnemyElementState state)
        {
            return state.Phase == EnemyElementPhase.Marked
                ? effectsByElement[(int)state.Element]
                : null;
        }

        private void ShowDesiredEffect()
        {
            if (activeEffect != null || desiredEffect == null)
            {
                return;
            }

            activeEffect = desiredEffect;
            scaleProgress = 0f;
            activeEffect.Root.transform.localScale = Vector3.zero;
            activeEffect.Root.SetActive(true);
            for (int index = 0; index < activeEffect.Particles.Length; index++)
            {
                activeEffect.Particles[index].Play(false);
            }
        }

        private void ResetEffects()
        {
            for (int index = 0; index < effectsByElement.Length; index++)
            {
                EffectRoot effect = effectsByElement[index];
                if (effect != null)
                {
                    Deactivate(effect);
                }
            }

            activeEffect = null;
            desiredEffect = null;
            scaleProgress = 0f;
        }

        private static void Deactivate(EffectRoot effect)
        {
            for (int index = 0; index < effect.Particles.Length; index++)
            {
                effect.Particles[index].Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            effect.Root.SetActive(false);
            effect.Root.transform.localScale = effect.AuthoredScale;
        }
    }
}
