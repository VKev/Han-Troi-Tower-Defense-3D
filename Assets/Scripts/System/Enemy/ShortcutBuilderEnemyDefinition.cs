using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.Enemies
{
    [CreateAssetMenu(
        fileName = "ShortcutBuilderEnemy",
        menuName = "Tower Defense/Enemies/Shortcut Builder Enemy")]
    public sealed class ShortcutBuilderEnemyDefinition : EnemyDefinition
    {
        [SerializeField, Min(0.01f)] private float channelDurationSeconds = 2.5f;

        public float ChannelDurationSeconds => channelDurationSeconds;

        internal override void CollectSpecificValidationErrors(ICollection<string> errors)
        {
            if (channelDurationSeconds <= 0f)
            {
                errors.Add($"{name}: Channel Duration must be greater than zero.");
            }
        }
    }
}
