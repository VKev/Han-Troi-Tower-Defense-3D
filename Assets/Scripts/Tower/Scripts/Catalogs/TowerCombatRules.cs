using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.Towers
{
    public enum DefenseResolutionStep
    {
        StrongestEarthReduction,
        PercentPenetration,
        FlatPenetration,
        ClampToMinimum,
        Mitigation,
        DamageTakenModifier
    }

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
        [SerializeField, Min(0f)] private float projectileCollisionRadiusMeters = 0.18f;

        [Header("Level Economy")]
        [SerializeField, Min(0)] private int startingGold = 250;
        [SerializeField, Range(0f, 1f)] private float sellRefundFraction = 0.7f;
        [SerializeField, Min(0)] private int normalWaveReward = 100;
        [SerializeField, Min(0)] private int bossWaveReward = 140;

        [Header("Progression Limits")]
        [SerializeField, Min(0)] private int maximumTierThreeElementTowers = 2;

        [Header("Defense Resolution")]
        [SerializeField, Min(0f)] private float minimumEffectiveDefense;
        [SerializeField] private DefenseResolutionStep[] defenseResolutionOrder =
        {
            DefenseResolutionStep.StrongestEarthReduction,
            DefenseResolutionStep.PercentPenetration,
            DefenseResolutionStep.FlatPenetration,
            DefenseResolutionStep.ClampToMinimum,
            DefenseResolutionStep.Mitigation,
            DefenseResolutionStep.DamageTakenModifier
        };

        public int MinimumProcessorCountInValidChain => minimumProcessorCountInValidChain;
        public int MinimumElementCountInValidChain => minimumElementCountInValidChain;
        public float MaximumLinkRangeMeters => maximumLinkRangeMeters;
        public int NormalQueueCapacity => normalQueueCapacity;
        public float MinimumProcessIntervalSeconds => minimumProcessIntervalSeconds;
        public float ProjectileSpeedMetersPerSecond => projectileSpeedMetersPerSecond;
        public float ProjectileCollisionRadiusMeters => projectileCollisionRadiusMeters;
        public int StartingGold => startingGold;
        public float SellRefundFraction => sellRefundFraction;
        public int NormalWaveReward => normalWaveReward;
        public int BossWaveReward => bossWaveReward;
        public int MaximumTierThreeElementTowers => maximumTierThreeElementTowers;
        public float MinimumEffectiveDefense => minimumEffectiveDefense;
        public IReadOnlyList<DefenseResolutionStep> DefenseResolutionOrder =>
            defenseResolutionOrder;

        public float CalculateProcessInterval(
            float baseIntervalSeconds,
            float totalProcessSpeedBonusFraction)
        {
            float calculated = baseIntervalSeconds / (1f + Mathf.Max(0f, totalProcessSpeedBonusFraction));
            return Mathf.Max(minimumProcessIntervalSeconds, calculated);
        }
    }
}
