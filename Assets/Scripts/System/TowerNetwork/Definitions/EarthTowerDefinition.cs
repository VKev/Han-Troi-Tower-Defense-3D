using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.Towers
{
    [CreateAssetMenu(
        fileName = "EarthTower",
        menuName = "Tower Defense/Towers/Elements/Earth")]
    public sealed class EarthTowerDefinition : ElementTowerDefinition
    {
        public enum AdaptiveDamageRule
        {
            HigherArmorDealsMagicHigherMagicResistanceDealsPhysical
        }

        [Serializable]
        public sealed class ArmorBreakProfile
        {
            [SerializeField, Range(0f, 1f)] private float armorReductionFraction;
            [SerializeField, Min(0f)] private float durationSeconds;
            [SerializeField] private EffectStackingRule stackingRule =
                EffectStackingRule.RefreshDurationWithoutStacking;

            public ArmorBreakProfile(float armorReductionFraction, float durationSeconds)
            {
                this.armorReductionFraction = armorReductionFraction;
                this.durationSeconds = durationSeconds;
            }

            public float ArmorReductionFraction => armorReductionFraction;
            public float DurationSeconds => durationSeconds;
            public EffectStackingRule StackingRule => stackingRule;
        }

        [Serializable]
        public sealed class TierOneProfile
        {
            [SerializeField, Min(1f)] private float damageMultiplier = 1.5f;
            [SerializeField, Min(1f)] private float areaRadiusMultiplier = 1.3f;

            public float DamageMultiplier => damageMultiplier;
            public float AreaRadiusMultiplier => areaRadiusMultiplier;
        }

        [Serializable]
        public sealed class ConvergenceBranchProfile
        {
            [SerializeField, Min(1f)] private float tierTwoDamageMultiplier = 1.1f;
            [SerializeField, Min(1f)] private float tierTwoIntervalMultiplier = 1.3f;
            [SerializeField, Min(1)] private int trajectoryCount = 2;
            [SerializeField] private bool bundleUsesOneQueueSlot = true;
            [SerializeField] private bool bundleSharesDirectHitSet = true;
            [SerializeField, Range(0f, 1f)] private float payloadFractionPerLane = 0.5f;
            [SerializeField, Min(1f)] private float convergenceDamageMultiplier = 3f;
            [SerializeField, Min(0f)] private float convergenceRadiusMeters = 2.75f;
            [SerializeField] private ArmorBreakProfile convergenceArmorBreak =
                new ArmorBreakProfile(0.5f, 4f);

            public float TierTwoDamageMultiplier => tierTwoDamageMultiplier;
            public float TierTwoIntervalMultiplier => tierTwoIntervalMultiplier;
            public int TrajectoryCount => trajectoryCount;
            public bool BundleUsesOneQueueSlot => bundleUsesOneQueueSlot;
            public bool BundleSharesDirectHitSet => bundleSharesDirectHitSet;
            public float PayloadFractionPerLane => payloadFractionPerLane;
            public float ConvergenceDamageMultiplier => convergenceDamageMultiplier;
            public float ConvergenceRadiusMeters => convergenceRadiusMeters;
            public ArmorBreakProfile ConvergenceArmorBreak => convergenceArmorBreak;
        }

        [Serializable]
        public sealed class WeaknessProfile
        {
            [SerializeField, Min(0f)] private float durationSeconds;
            [SerializeField, Range(0f, 1f)] private float physicalDamageTakenBonusFraction;
            [SerializeField, Range(0f, 1f)] private float magicDamageTakenBonusFraction;
            [SerializeField] private EffectStackingRule stackingRule =
                EffectStackingRule.RefreshDurationWithoutStacking;

            public WeaknessProfile(
                float durationSeconds,
                float physicalDamageTakenBonusFraction,
                float magicDamageTakenBonusFraction)
            {
                this.durationSeconds = durationSeconds;
                this.physicalDamageTakenBonusFraction = physicalDamageTakenBonusFraction;
                this.magicDamageTakenBonusFraction = magicDamageTakenBonusFraction;
            }

            public float DurationSeconds => durationSeconds;
            public float PhysicalDamageTakenBonusFraction =>
                physicalDamageTakenBonusFraction;
            public float MagicDamageTakenBonusFraction => magicDamageTakenBonusFraction;
            public EffectStackingRule StackingRule => stackingRule;
        }

        [Serializable]
        public sealed class AdaptiveBranchProfile
        {
            [SerializeField] private WeaknessProfile tierTwoWeakness =
                new WeaknessProfile(3f, 0.2f, 0.1f);
            [SerializeField, Min(0f)] private float evolutionProcessSpeedBonusFraction = 5f;
            [SerializeField, Range(0f, 1f)] private float evolutionDamageMultiplier = 0.5f;
            [SerializeField] private EqualDefenseDamageType equalDefenseDamageType =
                EqualDefenseDamageType.Physical;
            [SerializeField] private AdaptiveDamageRule adaptiveDamageRule =
                AdaptiveDamageRule.HigherArmorDealsMagicHigherMagicResistanceDealsPhysical;
            [SerializeField] private WeaknessProfile evolutionWeakness =
                new WeaknessProfile(3f, 0.2f, 0.05f);
            [SerializeField] private bool retainsBaseArmorBreak = true;

            public WeaknessProfile TierTwoWeakness => tierTwoWeakness;
            public float EvolutionProcessSpeedBonusFraction =>
                evolutionProcessSpeedBonusFraction;
            public float EvolutionDamageMultiplier => evolutionDamageMultiplier;
            public EqualDefenseDamageType EqualDefenseDamageType => equalDefenseDamageType;
            public AdaptiveDamageRule DamageRule => adaptiveDamageRule;
            public WeaknessProfile EvolutionWeakness => evolutionWeakness;
            public bool RetainsBaseArmorBreak => retainsBaseArmorBreak;
        }

        [SerializeField] private TowerCoreProfile core = new TowerCoreProfile(
            "earth",
            "Earth",
            new TowerNetworkProfile(1, 1, 3, true),
            new TowerThroughputProfile(1.1f, 1, 1),
            new TowerEconomyProfile(70, 0, 0, true));
        [SerializeField] private ElementUpgradeCostProfile upgradeCosts =
            new ElementUpgradeCostProfile();
        [SerializeField] private DamageProfile directDamage =
            new DamageProfile(6f, DamageType.Physical);
        [SerializeField, Min(0f)] private float baseAreaRadiusMeters = 1.75f;
        [SerializeField] private ArmorBreakProfile baseArmorBreak =
            new ArmorBreakProfile(0.3f, 2f);
        [SerializeField] private TierOneProfile tierOne = new TierOneProfile();
        [SerializeField] private ConvergenceBranchProfile convergenceBranch =
            new ConvergenceBranchProfile();
        [SerializeField] private AdaptiveBranchProfile adaptiveBranch =
            new AdaptiveBranchProfile();

        public override TowerFamily Family => TowerFamily.Earth;
        public override ElementType Element => ElementType.Earth;
        public override TowerCoreProfile Core => core;
        public override ElementUpgradeCostProfile UpgradeCosts => upgradeCosts;
        public DamageProfile DirectDamage => directDamage;
        public float BaseAreaRadiusMeters => baseAreaRadiusMeters;
        public ArmorBreakProfile BaseArmorBreak => baseArmorBreak;
        public TierOneProfile TierOne => tierOne;
        public ConvergenceBranchProfile ConvergenceBranch => convergenceBranch;
        public AdaptiveBranchProfile AdaptiveBranch => adaptiveBranch;

        internal override void CollectSpecificValidationErrors(List<string> errors)
        {
            if (directDamage == null || directDamage.Amount <= 0f || baseAreaRadiusMeters <= 0f)
            {
                errors.Add("Earth damage and AOE radius must be greater than zero.");
            }

            if (baseArmorBreak == null || baseArmorBreak.ArmorReductionFraction <= 0f)
            {
                errors.Add("Earth requires its base Physical Armor reduction.");
            }

            if (tierOne == null || tierOne.DamageMultiplier < 1f ||
                tierOne.AreaRadiusMultiplier < 1f)
            {
                errors.Add("Earth Tier 1 profile is incomplete.");
            }

            if (convergenceBranch == null || convergenceBranch.TrajectoryCount != 2 ||
                !convergenceBranch.BundleUsesOneQueueSlot ||
                !convergenceBranch.BundleSharesDirectHitSet ||
                convergenceBranch.PayloadFractionPerLane != 0.5f ||
                convergenceBranch.ConvergenceDamageMultiplier <= 0f ||
                convergenceBranch.ConvergenceArmorBreak == null)
            {
                errors.Add("Nham Tinh/Song Cực two-lane bundle contract is invalid.");
            }

            if (adaptiveBranch == null || adaptiveBranch.EvolutionProcessSpeedBonusFraction != 5f)
            {
                errors.Add("Sacrum Terra Process Speed bonus must be +500%.");
            }
            else if (adaptiveBranch.TierTwoWeakness == null ||
                     adaptiveBranch.EvolutionWeakness == null ||
                     !adaptiveBranch.RetainsBaseArmorBreak ||
                     adaptiveBranch.DamageRule !=
                     AdaptiveDamageRule.HigherArmorDealsMagicHigherMagicResistanceDealsPhysical)
            {
                errors.Add("Sacrum Terra adaptive damage/Weakness contract is incomplete.");
            }
        }
    }
}
