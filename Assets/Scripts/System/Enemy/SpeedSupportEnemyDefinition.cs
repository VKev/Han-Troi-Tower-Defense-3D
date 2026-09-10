using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.Enemies
{
    [CreateAssetMenu(
        fileName = "SpeedSupportEnemy",
        menuName = "Tower Defense/Enemies/Speed Support Enemy")]
    public sealed class SpeedSupportEnemyDefinition : EnemyDefinition
    {
        private const float AdditionalStackFraction = 0.25f;

        [SerializeField, Min(0.01f)] private float auraRadiusMeters = 3f;
        [SerializeField, Range(0f, 1f)] private float regularSpeedBonusFraction = 0.25f;
        [SerializeField, Range(0f, 1f)] private float miniBossSpeedBonusFraction = 0.10f;
        [SerializeField, Min(0f)] private float activationDelaySeconds = 1f;
        [SerializeField, Min(0.01f)] private float skillDurationSeconds = 2.5f;

        public float AuraRadiusMeters => auraRadiusMeters;
        public float RegularSpeedBonusFraction => regularSpeedBonusFraction;
        public float MiniBossSpeedBonusFraction => miniBossSpeedBonusFraction;
        public float ActivationDelaySeconds => activationDelaySeconds;
        public float SkillDurationSeconds => skillDurationSeconds;

        public static float CalculateStackedBonus(float firstBonus, int stackCount)
        {
            return stackCount <= 0
                ? 0f
                : firstBonus * (1f + AdditionalStackFraction * (stackCount - 1));
        }

        internal override void CollectSpecificValidationErrors(ICollection<string> errors)
        {
            if (auraRadiusMeters <= 0f)
            {
                errors.Add($"{name}: Aura Radius must be greater than zero.");
            }

            if (regularSpeedBonusFraction <= 0f ||
                miniBossSpeedBonusFraction <= 0f ||
                miniBossSpeedBonusFraction > regularSpeedBonusFraction)
            {
                errors.Add($"{name}: Speed bonuses must be positive and the Mini-boss bonus cannot exceed Regular.");
            }
        }
    }
}
