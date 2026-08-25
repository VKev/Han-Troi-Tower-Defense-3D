using UnityEngine;

namespace TowerDefense3D.Towers
{
    [DisallowMultipleComponent]
    public sealed class TowerProjectileView : MonoBehaviour
    {
        private const float MinimumTravelDistance = 0.001f;
        private ParticleSystem[] particleSystems;
        private TrailRenderer[] trailRenderers;
        private Quaternion authoredLocalRotation;
        private float retirementDelaySeconds;
        private bool isInitialized;

        public long ProjectileId { get; private set; }

        public void Initialize()
        {
            particleSystems = GetComponentsInChildren<ParticleSystem>(true);
            trailRenderers = GetComponentsInChildren<TrailRenderer>(true);
            authoredLocalRotation = transform.localRotation;

            for (int index = 0; index < particleSystems.Length; index++)
            {
                ParticleSystem.MainModule main = particleSystems[index].main;
                main.stopAction = ParticleSystemStopAction.None;
            }

            for (int index = 0; index < trailRenderers.Length; index++)
            {
                trailRenderers[index].autodestruct = false;
                retirementDelaySeconds = Mathf.Max(retirementDelaySeconds, trailRenderers[index].time);
            }

            isInitialized = true;
            ResetForPool();
        }

        public void Show(TowerProjectileSnapshot snapshot)
        {
            Show(
                snapshot.ProjectileId,
                new Vector3(snapshot.Position.X, snapshot.Position.Y, snapshot.Position.Z));
        }

        public void Show(long projectileId, Vector3 renderedPosition)
        {
            EnsureInitialized();
            ProjectileId = projectileId;
            transform.position = renderedPosition;
            transform.localRotation = authoredLocalRotation;

            ClearTrails();
            gameObject.SetActive(true);
            for (int index = 0; index < particleSystems.Length; index++)
            {
                particleSystems[index].Clear(false);
                particleSystems[index].Play(false);
            }
        }

        internal void SetPosition(Vector3 renderedPosition)
        {
            EnsureInitialized();
            Vector3 travelDirection = renderedPosition - transform.position;
            if (travelDirection.sqrMagnitude >= MinimumTravelDistance * MinimumTravelDistance)
            {
                Quaternion authoredWorldRotation = transform.parent != null
                    ? transform.parent.rotation * authoredLocalRotation
                    : authoredLocalRotation;
                Vector3 authoredWorldForward = authoredWorldRotation * Vector3.forward;
                transform.rotation =
                    Quaternion.FromToRotation(authoredWorldForward, travelDirection.normalized)
                    * authoredWorldRotation;
            }

            transform.position = renderedPosition;
        }

        internal float BeginRetirement()
        {
            EnsureInitialized();
            for (int index = 0; index < particleSystems.Length; index++)
            {
                particleSystems[index].Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }

            return retirementDelaySeconds;
        }

        public void ResetForPool()
        {
            EnsureInitialized();
            ProjectileId = 0L;
            for (int index = 0; index < particleSystems.Length; index++)
            {
                particleSystems[index].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            ClearTrails();
            transform.localPosition = Vector3.zero;
            transform.localRotation = authoredLocalRotation;
            gameObject.SetActive(false);
        }

        private void ClearTrails()
        {
            for (int index = 0; index < trailRenderers.Length; index++)
            {
                trailRenderers[index].Clear();
            }
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
