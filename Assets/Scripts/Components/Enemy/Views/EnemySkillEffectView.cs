using TowerDefense3D.Audio;
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

        [Tooltip("Cue raised with the effect, so the skill is heard at the instant it is seen rather than at the instant it was decided. Leave as None for a silent skill.")]
        [SerializeField] private SoundId skillSoundId = SoundId.None;

        private Vfx.GlobalEffectEmitterView emitter;
        private ISoundPlayer soundPlayer;
        private int renderedCastVersion;
        private float pendingDelaySeconds;
        private bool hasPendingEffect;

        public void ConfigureEmitter(
            Vfx.GlobalEffectEmitterView sharedEmitter,
            ISoundPlayer sharedSoundPlayer = null)
        {
            emitter = sharedEmitter;
            soundPlayer = sharedSoundPlayer;
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
                PlayEffect();
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
                PlayEffect();
            }
        }

        /// <summary>
        /// Puts the effect on screen and its cue in the air in the same frame, including after
        /// the authored delay, so a skill that is seen late is also heard late.
        /// </summary>
        private void PlayEffect()
        {
            emitter.Play(effectPrefab, anchor.position);
            if (skillSoundId != SoundId.None)
            {
                soundPlayer?.Play(skillSoundId);
            }
        }

        public void Release()
        {
            renderedCastVersion = 0;
            hasPendingEffect = false;
        }
    }
}
