using System;
using UnityEngine;

namespace TowerDefense3D.Towers
{
    public enum DamageType
    {
        Physical,
        Magic,
        True
    }

    public enum EqualDefenseDamageType
    {
        Physical,
        Magic
    }

    public enum EffectStackingRule
    {
        StrongerReplacesWeakerAndEqualRefreshes,
        RefreshDurationWithoutStacking,
        StrongestOnly
    }

    [Serializable]
    public sealed class DamageProfile
    {
        [SerializeField, Min(0f)] private float amount;
        [SerializeField] private DamageType damageType;

        public DamageProfile(float amount, DamageType damageType)
        {
            this.amount = amount;
            this.damageType = damageType;
        }

        public float Amount => amount;
        public DamageType DamageType => damageType;
    }

    [Serializable]
    public sealed class BurnProfile
    {
        [SerializeField, Min(0f)] private float damagePerTick;
        [SerializeField, Min(0.01f)] private float tickIntervalSeconds;
        [SerializeField, Min(0f)] private float durationSeconds;
        [SerializeField] private bool snapshotsDamageOnApply;
        [SerializeField] private EffectStackingRule stackingRule;

        public BurnProfile(
            float damagePerTick,
            float tickIntervalSeconds,
            float durationSeconds,
            bool snapshotsDamageOnApply)
        {
            this.damagePerTick = damagePerTick;
            this.tickIntervalSeconds = tickIntervalSeconds;
            this.durationSeconds = durationSeconds;
            this.snapshotsDamageOnApply = snapshotsDamageOnApply;
            stackingRule = EffectStackingRule.StrongerReplacesWeakerAndEqualRefreshes;
        }

        public float DamagePerTick => damagePerTick;
        public float TickIntervalSeconds => tickIntervalSeconds;
        public float DurationSeconds => durationSeconds;
        public bool SnapshotsDamageOnApply => snapshotsDamageOnApply;
        public EffectStackingRule StackingRule => stackingRule;
    }

    [Serializable]
    public sealed class SlowProfile
    {
        [SerializeField, Range(0f, 1f)] private float strengthFraction;
        [SerializeField, Min(0f)] private float durationSeconds;
        [SerializeField] private EffectStackingRule stackingRule;

        public SlowProfile(float strengthFraction, float durationSeconds)
        {
            this.strengthFraction = strengthFraction;
            this.durationSeconds = durationSeconds;
            stackingRule = EffectStackingRule.StrongerReplacesWeakerAndEqualRefreshes;
        }

        public float StrengthFraction => strengthFraction;
        public float DurationSeconds => durationSeconds;
        public EffectStackingRule StackingRule => stackingRule;
    }

    [Serializable]
    public sealed class AuraProfile
    {
        [SerializeField, Min(0f)] private float radiusMeters;
        [SerializeField] private bool requiresValidChain;
        [SerializeField] private bool affectsOwner;
        [SerializeField] private EffectStackingRule stackingRule;

        public AuraProfile(float radiusMeters, bool requiresValidChain, bool affectsOwner)
        {
            this.radiusMeters = radiusMeters;
            this.requiresValidChain = requiresValidChain;
            this.affectsOwner = affectsOwner;
            stackingRule = EffectStackingRule.StrongestOnly;
        }

        public float RadiusMeters => radiusMeters;
        public bool RequiresValidChain => requiresValidChain;
        public bool AffectsOwner => affectsOwner;
        public EffectStackingRule StackingRule => stackingRule;
    }

    [Serializable]
    public sealed class ControlResistanceProfile
    {
        [SerializeField, Range(0f, 1f)] private float normalMultiplier;
        [SerializeField, Range(0f, 1f)] private float miniBossMultiplier;
        [SerializeField, Range(0f, 1f)] private float bossMultiplier;

        public ControlResistanceProfile(
            float normalMultiplier,
            float miniBossMultiplier,
            float bossMultiplier)
        {
            this.normalMultiplier = normalMultiplier;
            this.miniBossMultiplier = miniBossMultiplier;
            this.bossMultiplier = bossMultiplier;
        }

        public float NormalMultiplier => normalMultiplier;
        public float MiniBossMultiplier => miniBossMultiplier;
        public float BossMultiplier => bossMultiplier;
    }
}
