using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.Enemies
{
    [CreateAssetMenu(
        fileName = "StealthEnemy",
        menuName = "Tower Defense/Enemies/Stealth Enemy")]
    public sealed class StealthEnemyDefinition : EnemyDefinition
    {
        [SerializeField, Min(0.01f)] private float revealDurationSeconds = 2f;

        public float RevealDurationSeconds => revealDurationSeconds;

        internal override void CollectSpecificValidationErrors(ICollection<string> errors)
        {
            if (revealDurationSeconds <= 0f)
            {
                errors.Add($"{name}: Reveal Duration must be greater than zero.");
            }

        }
    }
}
