using System;
using UnityEngine;

namespace TowerDefense3D.Towers
{
    [Serializable]
    public sealed class TowerUpgradeTierProfile
    {
        [SerializeField, Min(0)] private int cost;
        [SerializeField, Min(0f)] private float attackIntervalSeconds;
        [SerializeField, Min(0f)] private float projectileSpeedMetersPerSecond;
        [SerializeField, Min(0f)] private float rangeMeters;
        [SerializeField, Min(0f)] private float damageMultiplier = 1f;
        [SerializeField, Min(0f)] private float pushDistanceMeters;
        [SerializeField, Range(0f, 1f)] private float slowStrengthFraction;
        [SerializeField, Min(0f)] private float slowDurationSeconds;
        [SerializeField, Min(0f)] private float burnDamagePerTick;
        [SerializeField, Min(0f)] private float burnTickIntervalSeconds;
        [SerializeField, Min(0f)] private float burnDurationSeconds;

        public int Cost => cost;
        public float AttackIntervalSeconds => attackIntervalSeconds;
        public float ProjectileSpeedMetersPerSecond => projectileSpeedMetersPerSecond;
        public float RangeMeters => rangeMeters;
        public float DamageMultiplier => damageMultiplier;
        public float PushDistanceMeters => pushDistanceMeters;
        public float SlowStrengthFraction => slowStrengthFraction;
        public float SlowDurationSeconds => slowDurationSeconds;
        public float BurnDamagePerTick => burnDamagePerTick;
        public float BurnTickIntervalSeconds => burnTickIntervalSeconds;
        public float BurnDurationSeconds => burnDurationSeconds;
    }

    [Serializable]
    public sealed class TowerUpgradeCostProfile
    {
        [SerializeField] private TowerUpgradeTierProfile tierOne = new TowerUpgradeTierProfile();
        [SerializeField] private TowerUpgradeTierProfile tierTwo = new TowerUpgradeTierProfile();

        public TowerUpgradeTierProfile TierOne => tierOne;
        public TowerUpgradeTierProfile TierTwo => tierTwo;
        public int MaxLevel => tierTwo != null && tierTwo.Cost > 0
            ? 2
            : tierOne != null && tierOne.Cost > 0
                ? 1
                : 0;
        public bool IsUpgradable => MaxLevel > 0;

        public int CostToReach(int currentLevel)
        {
            TowerUpgradeTierProfile tier = GetTier(currentLevel + 1);
            return tier?.Cost ?? 0;
        }

        public TowerUpgradeTierProfile GetTier(int level)
        {
            return level switch
            {
                1 => tierOne,
                2 => tierTwo,
                _ => null
            };
        }
    }
}
