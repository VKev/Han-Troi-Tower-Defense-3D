using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.Towers
{
    [CreateAssetMenu(
        fileName = "FireTower",
        menuName = "Tower Defense/Towers/Elements/Fire")]
    public sealed class FireTowerDefinition : ElementTowerDefinition
    {
        [Serializable]
        public sealed class TierOneProfile
        {
            [SerializeField, Min(0)] private int queueCapacityBonus = 2;
            [SerializeField, Min(1)] private int outputProjectileCount = 3;
            [SerializeField, Min(1)] private int requiredDownstreamReservationCount = 3;
            [SerializeField, Min(0f)] private float sequenceSpacingSeconds = 0.08f;
            [SerializeField, Min(0f)] private float directFireDamageMultiplier = 1.2f;
            [SerializeField] private bool conservesTotalPhysicalDamage = true;
            [SerializeField] private bool conservesModifiedDirectFireDamage = true;
            [SerializeField] private bool projectilesHaveIndependentIdsAndHitSets = true;

            public int QueueCapacityBonus => queueCapacityBonus;
            public int OutputProjectileCount => outputProjectileCount;
            public int RequiredDownstreamReservationCount =>
                requiredDownstreamReservationCount;
            public float SequenceSpacingSeconds => sequenceSpacingSeconds;
            public float DirectFireDamageMultiplier => directFireDamageMultiplier;
            public bool ConservesTotalPhysicalDamage => conservesTotalPhysicalDamage;
            public bool ConservesModifiedDirectFireDamage => conservesModifiedDirectFireDamage;
            public bool ProjectilesHaveIndependentIdsAndHitSets =>
                projectilesHaveIndependentIdsAndHitSets;
        }

        [Serializable]
        public sealed class RapidBurnBranchProfile
        {
            [SerializeField, Min(0f)] private float burnDurationSeconds = 4f;
            [SerializeField, Min(0)] private int spreadTargetCount = 2;
            [SerializeField, Min(0f)] private float spreadRadiusMeters = 2.5f;
            [SerializeField] private AuraProfile evolutionAura =
                new AuraProfile(3f, true, false);
            [SerializeField, Min(0f)] private float auraLingerSeconds = 1.25f;
            [SerializeField, Range(0f, 1f)] private float auraBurnPotencyFraction = 0.5f;
            [SerializeField, Range(0f, 1f)] private float burnMagicResistanceIgnoreFraction = 0.25f;
            [SerializeField, Range(0f, 1f)] private float alliedFireMagicPenetrationFraction = 0.1f;

            public float BurnDurationSeconds => burnDurationSeconds;
            public int SpreadTargetCount => spreadTargetCount;
            public float SpreadRadiusMeters => spreadRadiusMeters;
            public AuraProfile EvolutionAura => evolutionAura;
            public float AuraLingerSeconds => auraLingerSeconds;
            public float AuraBurnPotencyFraction => auraBurnPotencyFraction;
            public float BurnMagicResistanceIgnoreFraction =>
                burnMagicResistanceIgnoreFraction;
            public float AlliedFireMagicPenetrationFraction =>
                alliedFireMagicPenetrationFraction;
        }

        [Serializable]
        public sealed class ExecutionBurnBranchProfile
        {
            [SerializeField, Min(0f)] private float tierTwoBurnDamageMultiplier = 1.25f;
            [SerializeField, Min(0f)] private float evolutionExtraBurnDurationSeconds = 2f;
            [SerializeField, Range(0f, 1f)] private float maxHealthDamageFraction = 0.02f;
            [SerializeField, Min(0f)] private float minimumMaxHealthDamage = 10f;
            [SerializeField, Min(0f)] private float maximumMaxHealthDamage = 50f;
            [SerializeField, Range(0f, 1f)] private float deathSpreadChance = 0.3f;
            [SerializeField, Min(1)] private int deathSpreadTargetCount = 2;
            [SerializeField, Min(0f)] private float deathSpreadRadiusMeters = 2.5f;
            [SerializeField] private AuraProfile evolutionAura =
                new AuraProfile(3f, true, false);
            [SerializeField, Min(0f)] private float auraLingerSeconds = 1.25f;
            [SerializeField, Range(0f, 1f)] private float auraBurnPotencyFraction = 0.5f;

            public float TierTwoBurnDamageMultiplier => tierTwoBurnDamageMultiplier;
            public float EvolutionExtraBurnDurationSeconds => evolutionExtraBurnDurationSeconds;
            public float MaxHealthDamageFraction => maxHealthDamageFraction;
            public float MinimumMaxHealthDamage => minimumMaxHealthDamage;
            public float MaximumMaxHealthDamage => maximumMaxHealthDamage;
            public float DeathSpreadChance => deathSpreadChance;
            public int DeathSpreadTargetCount => deathSpreadTargetCount;
            public float DeathSpreadRadiusMeters => deathSpreadRadiusMeters;
            public AuraProfile EvolutionAura => evolutionAura;
            public float AuraLingerSeconds => auraLingerSeconds;
            public float AuraBurnPotencyFraction => auraBurnPotencyFraction;
        }

        [SerializeField] private TowerCoreProfile core = new TowerCoreProfile(
            "fire",
            "Fire",
            new TowerNetworkProfile(1, 1, 3, true),
            new TowerThroughputProfile(0.85f, 1, 1),
            new TowerEconomyProfile(70, 0, 0, true));
        [SerializeField] private ElementUpgradeCostProfile upgradeCosts =
            new ElementUpgradeCostProfile();
        [SerializeField] private DamageProfile directDamage =
            new DamageProfile(5f, DamageType.Magic);
        [SerializeField] private BurnProfile burn = new BurnProfile(1f, 0.5f, 2f, true);
        [SerializeField] private TierOneProfile tierOne = new TierOneProfile();
        [SerializeField] private RapidBurnBranchProfile rapidBurnBranch =
            new RapidBurnBranchProfile();
        [SerializeField] private ExecutionBurnBranchProfile executionBurnBranch =
            new ExecutionBurnBranchProfile();

        public override TowerFamily Family => TowerFamily.Fire;
        public override ElementType Element => ElementType.Fire;
        public override TowerCoreProfile Core => core;
        public override ElementUpgradeCostProfile UpgradeCosts => upgradeCosts;
        public DamageProfile DirectDamage => directDamage;
        public BurnProfile Burn => burn;
        public TierOneProfile TierOne => tierOne;
        public RapidBurnBranchProfile RapidBurnBranch => rapidBurnBranch;
        public ExecutionBurnBranchProfile ExecutionBurnBranch => executionBurnBranch;

        internal override void CollectSpecificValidationErrors(List<string> errors)
        {
            if (directDamage == null || directDamage.Amount <= 0f)
            {
                errors.Add("Fire direct damage must be greater than zero.");
            }

            if (burn == null || burn.TickIntervalSeconds <= 0f || burn.DurationSeconds <= 0f)
            {
                errors.Add("Fire Burn duration and tick interval must be greater than zero.");
            }

            if (tierOne == null || tierOne.OutputProjectileCount != 3 ||
                tierOne.RequiredDownstreamReservationCount != tierOne.OutputProjectileCount ||
                tierOne.SequenceSpacingSeconds <= 0f ||
                tierOne.DirectFireDamageMultiplier < 1f ||
                !tierOne.ConservesTotalPhysicalDamage ||
                !tierOne.ConservesModifiedDirectFireDamage ||
                !tierOne.ProjectilesHaveIndependentIdsAndHitSets)
            {
                errors.Add("Fire Tier 1 projectile clone contract is invalid.");
            }

            if (rapidBurnBranch == null || rapidBurnBranch.BurnDurationSeconds <= 0f ||
                rapidBurnBranch.SpreadTargetCount <= 0 ||
                rapidBurnBranch.SpreadRadiusMeters <= 0f ||
                rapidBurnBranch.EvolutionAura == null ||
                rapidBurnBranch.AuraLingerSeconds <= 0f ||
                rapidBurnBranch.AuraBurnPotencyFraction <= 0f)
            {
                errors.Add("Fire Hỏa Tử/Chân Viêm branch is incomplete.");
            }

            if (executionBurnBranch == null ||
                executionBurnBranch.TierTwoBurnDamageMultiplier < 1f ||
                executionBurnBranch.MaximumMaxHealthDamage <
                executionBurnBranch.MinimumMaxHealthDamage ||
                executionBurnBranch.DeathSpreadChance <= 0f ||
                executionBurnBranch.DeathSpreadRadiusMeters <= 0f ||
                executionBurnBranch.EvolutionAura == null ||
                executionBurnBranch.AuraLingerSeconds <= 0f ||
                executionBurnBranch.AuraBurnPotencyFraction <= 0f)
            {
                errors.Add("Fire Diệt Viêm/Hỏa Ngục branch is incomplete.");
            }
        }
    }
}
