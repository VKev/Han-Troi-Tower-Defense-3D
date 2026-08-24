using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.Enemies
{
    [CreateAssetMenu(
        fileName = "SpeedSupportEnemy",
        menuName = "Tower Defense/Enemies/Speed Support Enemy")]
    public sealed class SpeedSupportEnemyDefinition : EnemyDefinition
    {
        [SerializeField, Min(0.01f)] private float auraRadiusMeters = 3f;
        [SerializeField, Range(0f, 1f)] private float regularSpeedBonusFraction = 0.25f;
        [SerializeField, Range(0f, 1f)] private float miniBossSpeedBonusFraction = 0.10f;

        public float AuraRadiusMeters => auraRadiusMeters;
        public float RegularSpeedBonusFraction => regularSpeedBonusFraction;
        public float MiniBossSpeedBonusFraction => miniBossSpeedBonusFraction;

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
