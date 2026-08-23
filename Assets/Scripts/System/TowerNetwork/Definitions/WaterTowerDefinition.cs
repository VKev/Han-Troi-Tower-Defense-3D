using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.Towers
{
    [CreateAssetMenu(
        fileName = "WaterTower",
        menuName = "Tower Defense/Towers/Elements/Water")]
    public sealed class WaterTowerDefinition : ElementTowerDefinition
    {
        [Serializable]
        public sealed class TierOneProfile
        {
            [SerializeField, Min(1f)] private float projectileRadiusMultiplier = 1.1f;
            [SerializeField, Min(0)] private int secondarySlowTargetCount = 1;
            [SerializeField, Min(0)] private int queueCapacityBonus = 1;
            [SerializeField, Min(0f)] private float processSpeedBonusFraction = 0.15f;
            [SerializeField, Min(0f)] private float secondarySearchRadiusMeters = 2.5f;

            public float ProjectileRadiusMultiplier => projectileRadiusMultiplier;
            public int SecondarySlowTargetCount => secondarySlowTargetCount;
            public int QueueCapacityBonus => queueCapacityBonus;
            public float ProcessSpeedBonusFraction => processSpeedBonusFraction;
            public float SecondarySearchRadiusMeters => secondarySearchRadiusMeters;
        }

        [Serializable]
        public sealed class WaterStackBranchProfile
        {
            [SerializeField, Min(0f)] private float stackDurationSeconds = 2.5f;
            [SerializeField, Min(1)] private int stackThreshold = 3;
            [SerializeField, Min(0f)] private float stunDurationSeconds = 0.5f;
            [SerializeField, Min(0f)] private float bossStunDurationSeconds = 0.15f;
            [SerializeField, Min(0f)] private float stunInternalCooldownSeconds = 2f;
            [SerializeField, Min(1)] private int evolutionShardCount = 6;
            [SerializeField, Min(0f)] private float evolutionPhysicalTotalMultiplier = 1f;
            [SerializeField, Min(0f)] private float evolutionWaterTotalMultiplier = 2f;
            [SerializeField, Min(0f)] private float evolutionProcessSpeedBonusFraction = 0.2f;
            [SerializeField] private bool bundleUsesOneQueueSlot = true;
            [SerializeField] private bool bundleSharesDirectHitSet = true;
            [SerializeField] private bool oneEnemyTakesDamageFromAtMostOneShard = true;
            [SerializeField] private bool eachShardAppliesSlowAndWaterStack = true;

            public float StackDurationSeconds => stackDurationSeconds;
            public int StackThreshold => stackThreshold;
            public float StunDurationSeconds => stunDurationSeconds;
            public float BossStunDurationSeconds => bossStunDurationSeconds;
            public float StunInternalCooldownSeconds => stunInternalCooldownSeconds;
            public int EvolutionShardCount => evolutionShardCount;
            public float EvolutionPhysicalTotalMultiplier => evolutionPhysicalTotalMultiplier;
            public float EvolutionWaterTotalMultiplier => evolutionWaterTotalMultiplier;
            public float EvolutionProcessSpeedBonusFraction =>
                evolutionProcessSpeedBonusFraction;
            public bool BundleUsesOneQueueSlot => bundleUsesOneQueueSlot;
            public bool BundleSharesDirectHitSet => bundleSharesDirectHitSet;
            public bool OneEnemyTakesDamageFromAtMostOneShard =>
                oneEnemyTakesDamageFromAtMostOneShard;
            public bool EachShardAppliesSlowAndWaterStack =>
                eachShardAppliesSlowAndWaterStack;
        }

        [Serializable]
        public sealed class PressureBranchProfile
        {
            [SerializeField, Min(0f)] private float tierTwoDamageMultiplier = 1.1f;
            [SerializeField, Min(0f)] private float tierTwoProcessSpeedBonusFraction = 0.4f;
            [SerializeField] private SlowProfile evolutionSlow = new SlowProfile(0.7f, 1f);
            [SerializeField, Min(1f)] private float slowFieldRadiusMultiplier = 1.2f;
            [SerializeField, Min(0f)] private float baseSplashRadiusMeters = 2.5f;
            [SerializeField, Min(0)] private int secondaryTargetCount = 5;
            [SerializeField, Range(0f, 1f)] private float bossEffectMultiplier = 0.5f;
            [SerializeField] private bool evolutionDealsDamage;

            public float TierTwoDamageMultiplier => tierTwoDamageMultiplier;
            public float TierTwoProcessSpeedBonusFraction =>
                tierTwoProcessSpeedBonusFraction;
            public SlowProfile EvolutionSlow => evolutionSlow;
            public float SlowFieldRadiusMultiplier => slowFieldRadiusMultiplier;
            public float BaseSplashRadiusMeters => baseSplashRadiusMeters;
            public int SecondaryTargetCount => secondaryTargetCount;
            public float BossEffectMultiplier => bossEffectMultiplier;
            public bool EvolutionDealsDamage => evolutionDealsDamage;
        }

        [SerializeField] private TowerCoreProfile core = new TowerCoreProfile(
            "water",
            "Water",
            new TowerNetworkProfile(1, 1, 3, true),
            new TowerThroughputProfile(0.85f, 1, 1),
            new TowerEconomyProfile(70, 0, 0, true));
        [SerializeField] private ElementUpgradeCostProfile upgradeCosts =
            new ElementUpgradeCostProfile();
        [SerializeField] private DamageProfile directDamage =
            new DamageProfile(5f, DamageType.Magic);
        [SerializeField] private SlowProfile slow = new SlowProfile(0.3f, 1f);
        [SerializeField, Range(0f, 1f)] private float bossSlowEffectMultiplier = 0.5f;
        [SerializeField] private TierOneProfile tierOne = new TierOneProfile();
        [SerializeField] private WaterStackBranchProfile waterStackBranch =
            new WaterStackBranchProfile();
        [SerializeField] private PressureBranchProfile pressureBranch =
            new PressureBranchProfile();
        [SerializeField] private AuraProfile evolutionAura = new AuraProfile(4.5f, true, false);
        [SerializeField, Min(0f)] private float alliedProjectileRadiusBonusFraction = 0.15f;

        public override TowerFamily Family => TowerFamily.Water;
        public override ElementType Element => ElementType.Water;
        public override TowerCoreProfile Core => core;
        public override ElementUpgradeCostProfile UpgradeCosts => upgradeCosts;
        public DamageProfile DirectDamage => directDamage;
        public SlowProfile Slow => slow;
        public float BossSlowEffectMultiplier => bossSlowEffectMultiplier;
        public TierOneProfile TierOne => tierOne;
        public WaterStackBranchProfile WaterStackBranch => waterStackBranch;
        public PressureBranchProfile PressureBranch => pressureBranch;
        public AuraProfile EvolutionAura => evolutionAura;
        public float AlliedProjectileRadiusBonusFraction =>
            alliedProjectileRadiusBonusFraction;

        internal override void CollectSpecificValidationErrors(List<string> errors)
        {
            if (directDamage == null || directDamage.Amount <= 0f)
            {
                errors.Add("Water direct damage must be greater than zero.");
            }

            if (slow == null || slow.StrengthFraction <= 0f || slow.DurationSeconds <= 0f)
            {
                errors.Add("Water Slow strength and duration must be greater than zero.");
            }

            if (tierOne == null || tierOne.ProjectileRadiusMultiplier < 1f ||
                tierOne.SecondarySlowTargetCount <= 0 ||
                tierOne.ProcessSpeedBonusFraction <= 0f)
            {
                errors.Add("Water Tier 1 profile is incomplete.");
            }

            if (waterStackBranch == null || waterStackBranch.EvolutionShardCount != 6 ||
                waterStackBranch.StackThreshold != 3 ||
                waterStackBranch.StunInternalCooldownSeconds <= 0f ||
                !waterStackBranch.BundleUsesOneQueueSlot ||
                !waterStackBranch.BundleSharesDirectHitSet ||
                !waterStackBranch.OneEnemyTakesDamageFromAtMostOneShard ||
                !waterStackBranch.EachShardAppliesSlowAndWaterStack)
            {
                errors.Add("Water Sa Phất/Lục Phiến bundle contract is invalid.");
            }

            if (pressureBranch == null || pressureBranch.EvolutionDealsDamage ||
                pressureBranch.EvolutionSlow == null ||
                (slow != null &&
                 pressureBranch.EvolutionSlow.StrengthFraction <= slow.StrengthFraction) ||
                pressureBranch.SecondaryTargetCount != 5 ||
                pressureBranch.SlowFieldRadiusMultiplier <= 1f)
            {
                errors.Add("Water Giáng Lưu must replace projectile damage with control.");
            }
        }
    }
}
