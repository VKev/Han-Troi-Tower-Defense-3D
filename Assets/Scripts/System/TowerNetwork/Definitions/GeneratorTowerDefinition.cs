using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.Towers
{
    [CreateAssetMenu(
        fileName = "GeneratorTower",
        menuName = "Tower Defense/Towers/Source/Generator")]
    public sealed class GeneratorTowerDefinition : TowerCombatDefinition
    {
        [Serializable]
        public sealed class GenerationProfile
        {
            [SerializeField] private DamageProfile basicDamage =
                new DamageProfile(8f);
            [SerializeField] private bool generatesWithoutEnemy = true;
            [SerializeField] private bool requiresValidChain = true;
            [SerializeField, Min(1)] private int upgradedBatchSize = 2;
            [SerializeField] private bool upgradedProjectilesHaveIndependentIds = true;

            public DamageProfile BasicDamage => basicDamage;
            public bool GeneratesWithoutEnemy => generatesWithoutEnemy;
            public bool RequiresValidChain => requiresValidChain;
            public int UpgradedBatchSize => upgradedBatchSize;
            public bool UpgradedProjectilesHaveIndependentIds =>
                upgradedProjectilesHaveIndependentIds;
        }

        [SerializeField] private TowerCoreProfile core = new TowerCoreProfile(
            "generator",
            "Generator",
            new TowerNetworkProfile(0, 1, 0, false),
            new TowerThroughputProfile(1f, 1, 1, 0.08f),
            new TowerEconomyProfile(90, 80, 0, true));
        [SerializeField] private GenerationProfile generation = new GenerationProfile();

        public override TowerFamily Family => TowerFamily.Generator;
        public override TowerNetworkRole NetworkRole => TowerNetworkRole.Source;
        public override TowerCoreProfile Core => core;
        public GenerationProfile Generation => generation;

        internal override void CollectSpecificValidationErrors(List<string> errors)
        {
            if (generation == null)
            {
                errors.Add("Generator profile is missing.");
                return;
            }

            if (generation.BasicDamage == null || generation.BasicDamage.Amount <= 0f)
            {
                errors.Add("Generator Basic Damage must be greater than zero.");
            }

            if (Core?.Throughput != null &&
                generation.UpgradedBatchSize <= Core.Throughput.BatchSize)
            {
                errors.Add("Generator upgraded Batch Size must exceed its base Batch Size.");
            }
        }
    }
}
