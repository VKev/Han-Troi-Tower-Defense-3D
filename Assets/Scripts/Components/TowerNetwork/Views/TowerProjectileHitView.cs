using UnityEngine;

namespace TowerDefense3D.Towers
{
    [DisallowMultipleComponent]
    public sealed class TowerProjectileHitView : MonoBehaviour
    {
        private const float MinimumPlaybackSeconds = 0.05f;
        private ParticleSystem[] particleSystems;
        private Quaternion authoredLocalRotation;
        private float playbackDurationSeconds;
        private bool isInitialized;

        public void Initialize()
        {
            particleSystems = GetComponentsInChildren<ParticleSystem>(true);
            authoredLocalRotation = transform.localRotation;
            playbackDurationSeconds = MinimumPlaybackSeconds;

            for (int index = 0; index < particleSystems.Length; index++)
            {
                ParticleSystem.MainModule main = particleSystems[index].main;
                main.stopAction = ParticleSystemStopAction.None;
                playbackDurationSeconds = Mathf.Max(
                    playbackDurationSeconds,
                    main.startDelay.constantMax + main.duration + main.startLifetime.constantMax);
            }

            isInitialized = true;
            ResetForPool();
        }

        public float Play(Vector3 position)
        {
            EnsureInitialized();
            transform.position = position;
            transform.localRotation = authoredLocalRotation;
            gameObject.SetActive(true);

            for (int index = 0; index < particleSystems.Length; index++)
            {
                particleSystems[index].Clear(true);
                particleSystems[index].Play(true);
            }

            return playbackDurationSeconds;
        }

        public void ResetForPool()
        {
            EnsureInitialized();
            for (int index = 0; index < particleSystems.Length; index++)
            {
                particleSystems[index].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            transform.localPosition = Vector3.zero;
            transform.localRotation = authoredLocalRotation;
            gameObject.SetActive(false);
        }

        private void EnsureInitialized()
        {
            if (!isInitialized)
            {
                Initialize();
            }
        }
    }
}
