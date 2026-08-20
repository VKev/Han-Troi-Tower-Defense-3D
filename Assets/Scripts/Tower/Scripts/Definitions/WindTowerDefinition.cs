using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.Towers
{
    [CreateAssetMenu(
        fileName = "WindTower",
        menuName = "Tower Defense/Towers/Elements/Wind")]
    public sealed class WindTowerDefinition : ElementTowerDefinition
    {
        [Serializable]
        public sealed class TierOneProfile
        {
            [SerializeField, Min(0f)] private float processSpeedBonusFraction = 0.2f;
            [SerializeField, Min(1f)] private float pushDistanceMultiplier = 1.1f;
            [SerializeField, Min(1f)] private float windDamageMultiplier = 1.1f;

            public float ProcessSpeedBonusFraction => processSpeedBonusFraction;
            public float PushDistanceMultiplier => pushDistanceMultiplier;
            public float WindDamageMultiplier => windDamageMultiplier;
        }

        [Serializable]
        public sealed class CriticalBranchProfile
        {
            [SerializeField, Min(0f)] private float tierTwoProcessSpeedBonusFraction = 0.3f;
            [SerializeField, Range(0f, 1f)] private float tierTwoCriticalChance = 0.1f;
            [SerializeField, Min(1f)] private float criticalDamageMultiplier = 1.5f;
            [SerializeField, Min(0f)] private float evolutionProcessSpeedBonusFraction = 0.5f;
            [SerializeField, Range(0f, 1f)] private float evolutionCriticalChance = 1f;
            [SerializeField] private AuraProfile evolutionAura =
                new AuraProfile(4.5f, true, false);
            [SerializeField, Min(0f)] private float alliedWindProcessSpeedBonusFraction = 0.2f;

            public float TierTwoProcessSpeedBonusFraction =>
                tierTwoProcessSpeedBonusFraction;
            public float TierTwoCriticalChance => tierTwoCriticalChance;
            public float CriticalDamageMultiplier => criticalDamageMultiplier;
            public float EvolutionProcessSpeedBonusFraction =>
                evolutionProcessSpeedBonusFraction;
            public float EvolutionCriticalChance => evolutionCriticalChance;
            public AuraProfile EvolutionAura => evolutionAura;
            public float AlliedWindProcessSpeedBonusFraction =>
                alliedWindProcessSpeedBonusFraction;
        }

        [Serializable]
        public sealed class ControlBranchProfile
        {
            [SerializeField, Min(1f)] private float tierTwoPushDistanceMultiplier = 1.3f;
            [SerializeField, Min(0f)] private float gustRadiusMeters = 2.5f;
            [SerializeField, Min(1)] private int gustMaxTargets = 5;
            [SerializeField, Min(1f)] private float evolutionLinkRangeMultiplier = 1.3f;
            [SerializeField, Min(0f)] private float tornadoRadiusMeters = 3f;
            [SerializeField, Min(0f)] private float tornadoDurationSeconds = 1.5f;
            [SerializeField, Min(0f)] private float tornadoPushDistanceMeters = 2f;
            [SerializeField, Min(0f)] private float tornadoPerEnemyCooldownSeconds = 3f;
            [SerializeField, Min(0f)] private float globalBaseHitCooldownSeconds = 30f;
            [SerializeField, Min(0f)] private float normalLevitateSeconds = 4f;
            [SerializeField, Min(0f)] private float miniBossLevitateSeconds = 2f;
            [SerializeField, Min(0f)] private float bossStunSeconds = 1f;

            public float TierTwoPushDistanceMultiplier => tierTwoPushDistanceMultiplier;
            public float GustRadiusMeters => gustRadiusMeters;
            public int GustMaxTargets => gustMaxTargets;
            public float EvolutionLinkRangeMultiplier => evolutionLinkRangeMultiplier;
            public float TornadoRadiusMeters => tornadoRadiusMeters;
            public float TornadoDurationSeconds => tornadoDurationSeconds;
            public float TornadoPushDistanceMeters => tornadoPushDistanceMeters;
            public float TornadoPerEnemyCooldownSeconds => tornadoPerEnemyCooldownSeconds;
            public float GlobalBaseHitCooldownSeconds => globalBaseHitCooldownSeconds;
            public float NormalLevitateSeconds => normalLevitateSeconds;
            public float MiniBossLevitateSeconds => miniBossLevitateSeconds;
            public float BossStunSeconds => bossStunSeconds;
        }

        [SerializeField] private TowerCoreProfile core = new TowerCoreProfile(
            "wind",
            "Wind",
            new TowerNetworkProfile(1, 1, 3, true),
            new TowerThroughputProfile(0.85f, 1, 1),
            new TowerEconomyProfile(70, 0, 0, true));
        [SerializeField] private ElementUpgradeCostProfile upgradeCosts =
            new ElementUpgradeCostProfile();
        [SerializeField] private DamageProfile directDamage =
            new DamageProfile(5f, DamageType.Magic);
        [SerializeField, Min(0f)] private float basePushDistanceMeters = 0.5f;
        [SerializeField] private ControlResistanceProfile pushResistance =
            new ControlResistanceProfile(1f, 0.25f, 0f);
        [SerializeField] private TierOneProfile tierOne = new TierOneProfile();
        [SerializeField] private CriticalBranchProfile criticalBranch =
            new CriticalBranchProfile();
        [SerializeField] private ControlBranchProfile controlBranch =
            new ControlBranchProfile();

        public override TowerFamily Family => TowerFamily.Wind;
        public override ElementType Element => ElementType.Wind;
        public override TowerCoreProfile Core => core;
        public override ElementUpgradeCostProfile UpgradeCosts => upgradeCosts;
        public DamageProfile DirectDamage => directDamage;
        public float BasePushDistanceMeters => basePushDistanceMeters;
        public ControlResistanceProfile PushResistance => pushResistance;
        public TierOneProfile TierOne => tierOne;
        public CriticalBranchProfile CriticalBranch => criticalBranch;
        public ControlBranchProfile ControlBranch => controlBranch;

        internal override void CollectSpecificValidationErrors(List<string> errors)
        {
            if (directDamage == null || directDamage.Amount <= 0f)
            {
                errors.Add("Wind direct damage must be greater than zero.");
            }

            if (basePushDistanceMeters <= 0f || pushResistance == null)
            {
                errors.Add("Wind requires a positive path push and resistance profile.");
            }

            if (tierOne == null || tierOne.ProcessSpeedBonusFraction <= 0f ||
                tierOne.PushDistanceMultiplier < 1f || tierOne.WindDamageMultiplier < 1f)
            {
                errors.Add("Wind Tier 1 profile is incomplete.");
            }

            if (criticalBranch == null || criticalBranch.EvolutionCriticalChance != 1f ||
                criticalBranch.CriticalDamageMultiplier < 1f ||
                criticalBranch.EvolutionAura == null)
            {
                errors.Add("Phá Ngã – Lung must make every Wind direct hit critical.");
            }

            if (controlBranch == null || controlBranch.GlobalBaseHitCooldownSeconds <= 0f ||
                controlBranch.GustMaxTargets <= 0 || controlBranch.GustRadiusMeters <= 0f ||
                controlBranch.TornadoRadiusMeters <= 0f ||
                controlBranch.TornadoDurationSeconds <= 0f)
            {
                errors.Add("Di Hư – Phong Đột requires a shared global cooldown.");
            }
        }
    }
}
