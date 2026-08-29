using UnityEngine;

namespace TowerDefense3D.Enemies
{
    /// <summary>
    /// Keeps the speed-buff trail centred on the enemy model while the pooled view is active.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemySpeedTrailView : MonoBehaviour
    {
        [SerializeField] private GameObject trailPrefab;

        private GameObject trailRoot;
        private TrailRenderer[] trailRenderers;
        private AudioSource[] audioSources;
        private Vector3 bodyCenterLocal;
        private bool hasBodyCenter;

        public void Bind()
        {
            SetVisible(false);
            hasBodyCenter = false;
        }

        public void Render(bool isSpeedBuffed)
        {
            SetVisible(isSpeedBuffed);
        }

        public void Release()
        {
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (!visible)
            {
                if (trailRoot == null || !trailRoot.activeSelf)
                {
                    return;
                }

                for (int index = 0; index < trailRenderers.Length; index++)
                {
                    trailRenderers[index].Clear();
                }

                for (int index = 0; index < audioSources.Length; index++)
                {
                    audioSources[index].Stop();
                }

                trailRoot.SetActive(false);
                return;
            }

            EnsureTrail();
            if (trailRoot == null)
            {
                return;
            }

            trailRoot.SetActive(true);
        }

        private void EnsureTrail()
        {
            if (trailRoot != null || trailPrefab == null)
            {
                return;
            }

            if (!hasBodyCenter)
            {
                bodyCenterLocal = FindBodyCenterLocal();
                hasBodyCenter = true;
            }

            trailRoot = Instantiate(trailPrefab, transform);
            trailRoot.name = trailPrefab.name + " (speed buff)";
            trailRoot.transform.localPosition = bodyCenterLocal;
            trailRoot.transform.localRotation = Quaternion.identity;
            trailRenderers = trailRoot.GetComponentsInChildren<TrailRenderer>(true);
            audioSources = trailRoot.GetComponentsInChildren<AudioSource>(true);
            trailRoot.SetActive(false);
        }

        private Vector3 FindBodyCenterLocal()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            Bounds bounds = default;
            bool hasBounds = false;
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer is ParticleSystemRenderer || renderer is TrailRenderer
                    || renderer.GetComponentInParent<EnemyElementStatusView>() != null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds ? transform.InverseTransformPoint(bounds.center) : Vector3.zero;
        }
    }
}
