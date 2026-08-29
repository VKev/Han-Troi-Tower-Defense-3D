using UnityEngine;

namespace TowerDefense3D.Enemies
{
    /// <summary>
    /// Emits an enemy skill effect from an authored bone or spawn point using the pool's shared
    /// global emitter.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemySkillEffectView : MonoBehaviour
    {
        [SerializeField] private GameObject effectPrefab;
        [SerializeField] private Transform anchor;
        [SerializeField, Min(0f)] private float playDelaySeconds;

        private Vfx.GlobalEffectEmitterView emitter;
        private int renderedCastVersion;
        private float pendingDelaySeconds;
        private bool hasPendingEffect;

        public void ConfigureEmitter(Vfx.GlobalEffectEmitterView sharedEmitter)
        {
            emitter = sharedEmitter;
        }

        public void Bind(int skillCastVersion)
        {
            renderedCastVersion = skillCastVersion;
            hasPendingEffect = false;
        }

        public void Play(int skillCastVersion)
        {
            if (skillCastVersion == renderedCastVersion)
            {
                return;
            }

            renderedCastVersion = skillCastVersion;
            if (emitter == null || effectPrefab == null || anchor == null)
            {
                return;
            }

            if (playDelaySeconds <= 0f)
            {
                emitter.Play(effectPrefab, anchor.position);
                return;
            }

            pendingDelaySeconds = playDelaySeconds;
            hasPendingEffect = true;
        }

        private void Update()
        {
            if (!hasPendingEffect)
            {
                return;
            }

            pendingDelaySeconds -= Time.deltaTime;
            if (pendingDelaySeconds > 0f)
            {
                return;
            }

            hasPendingEffect = false;
            if (emitter != null && effectPrefab != null && anchor != null)
            {
                emitter.Play(effectPrefab, anchor.position);
            }
        }

        public void Release()
        {
            renderedCastVersion = 0;
            hasPendingEffect = false;
        }
    }
}
