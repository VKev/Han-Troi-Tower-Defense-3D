using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.Towers
{
    [CreateAssetMenu(
        fileName = "FireTower",
        menuName = "Tower Defense/Towers/Elements/Fire")]
    public sealed class FireTowerDefinition : ElementTowerDefinition
    {
        [SerializeField] private TowerCoreProfile core = new TowerCoreProfile(
            "fire",
            "Fire",
            new TowerNetworkProfile(1, 1, 3, true),
            new TowerThroughputProfile(0.85f, 1, 1),
            new TowerEconomyProfile(70, 0, 0, true));
        [SerializeField] private ElementUpgradeCostProfile upgradeCosts =
            new ElementUpgradeCostProfile();
        [SerializeField] private DamageProfile directDamage = new DamageProfile(5f);
        [SerializeField] private BurnProfile burn = new BurnProfile(1f, 0.5f, 2f, true);

        public override TowerFamily Family => TowerFamily.Fire;
        public override ElementType Element => ElementType.Fire;
        public override TowerCoreProfile Core => core;
        public override ElementUpgradeCostProfile UpgradeCosts => upgradeCosts;
        public DamageProfile DirectDamage => directDamage;
        public BurnProfile Burn => burn;

        internal override void CollectSpecificValidationErrors(List<string> errors)
        {
            if (directDamage == null || directDamage.Amount <= 0f)
            {
                errors.Add("Fire direct damage must be greater than zero.");
            }

            if (burn == null || burn.DamagePerTick <= 0f
                || burn.TickIntervalSeconds <= 0f || burn.DurationSeconds <= 0f)
            {
                errors.Add("Fire burn requires positive damage, interval, and duration.");
            }
        }
    }
}
