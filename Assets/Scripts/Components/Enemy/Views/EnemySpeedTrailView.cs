using UnityEngine;

namespace TowerDefense3D.Enemies
{
    /// <summary>
    /// Keeps the speed-buff trail centred on the enemy model while the pooled view is active.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemySpeedTrailView : MonoBehaviour
    {
        [SerializeField] private GameObject trailRoot;

        private TrailRenderer[] trailRenderers;
        private AudioSource[] audioSources;

        public void Bind()
        {
            SetVisible(false);
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
            if (trailRoot == null)
            {
                throw new MissingReferenceException("EnemySpeedTrailView requires an authored trail child.");
            }

            trailRenderers = trailRoot.GetComponentsInChildren<TrailRenderer>(true);
            audioSources = trailRoot.GetComponentsInChildren<AudioSource>(true);
            trailRoot.SetActive(false);
        }
    }
}
