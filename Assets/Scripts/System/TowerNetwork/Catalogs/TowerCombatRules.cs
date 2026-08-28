using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.Towers
{
    /// <summary>
    /// Global combat authoring defaults. Element reactions are intentionally absent:
    /// the latest Element processor overwrites the projectile element payload, while
    /// status effects already applied to enemies continue independently.
    /// </summary>
    [CreateAssetMenu(
        fileName = "TowerCombatRules",
        menuName = "Tower Defense/Towers/Combat Rules")]
    public sealed class TowerCombatRules : ScriptableObject
    {
        [Header("Network")]
        [SerializeField, Min(0)] private int minimumProcessorCountInValidChain;
        [SerializeField, Min(0)] private int minimumElementCountInValidChain;
        [SerializeField, Min(0f)] private float maximumLinkRangeMeters = 12f;
        [SerializeField, Min(1)] private int normalQueueCapacity = 3;
        [SerializeField, Min(0.01f)] private float minimumProcessIntervalSeconds = 0.15f;

        [Header("Projectile")]
        [SerializeField, Min(0f)] private float projectileSpeedMetersPerSecond = 10f;

        [Header("Level Economy")]
        [SerializeField, Range(0f, 1f)] private float sellRefundFraction = 0.7f;

        [Header("Progression Limits")]
        [SerializeField, Min(0)] private int maximumTierThreeElementTowers = 2;

        [Header("Simulation")]
        [SerializeField, Min(0.0001f)]
        private float simulationTickSeconds = 0.05f;

        [Tooltip("Ceiling on how fast knockback may drag an enemy backwards, as a fraction "
            + "of that enemy's own move speed. Below 1 the enemy always nets forward "
            + "progress no matter how many pushing towers fire at it.")]
        [SerializeField, Range(0f, 0.9f)]
        private float maximumPushSpeedFraction = 0.4f;

        public float SimulationTickSeconds => simulationTickSeconds;
        public float MaximumPushSpeedFraction => maximumPushSpeedFraction;

        public int MinimumProcessorCountInValidChain => minimumProcessorCountInValidChain;
        public int MinimumElementCountInValidChain => minimumElementCountInValidChain;
        public float MaximumLinkRangeMeters => maximumLinkRangeMeters;
        public int NormalQueueCapacity => normalQueueCapacity;
        public float MinimumProcessIntervalSeconds => minimumProcessIntervalSeconds;
        public float ProjectileSpeedMetersPerSecond => projectileSpeedMetersPerSecond;
        public float SellRefundFraction => sellRefundFraction;
        public int MaximumTierThreeElementTowers => maximumTierThreeElementTowers;
        public float CalculateProcessInterval(
            float baseIntervalSeconds,
            float totalProcessSpeedBonusFraction)
        {
            float calculated = baseIntervalSeconds / (1f + Mathf.Max(0f, totalProcessSpeedBonusFraction));
            return Mathf.Max(minimumProcessIntervalSeconds, calculated);
        }
    }
}
