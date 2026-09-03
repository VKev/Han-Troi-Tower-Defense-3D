using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.Towers
{
    [CreateAssetMenu(
        fileName = "WaterTower",
        menuName = "Tower Defense/Towers/Elements/Water")]
    public sealed class WaterTowerDefinition : ElementTowerDefinition
    {
        [SerializeField] private TowerCoreProfile core = new TowerCoreProfile(
            "water",
            "Water",
            new TowerNetworkProfile(1, 1, 3, true),
            new TowerThroughputProfile(0.85f, 1, 1),
            new TowerEconomyProfile(70, 0, 0, true));
        [SerializeField] private ElementUpgradeCostProfile upgradeCosts =
            new ElementUpgradeCostProfile();

        // Authored at zero on purpose: Water's identity is revealing Stealth, not damage.
        // The knobs exist so the Game Balance Center can tune it without a code change, and
        // zero here reads as "no damage yet" rather than "this tower cannot deal damage".
        // The tick interval sits at its field minimum because a burn that ticks every zero
        // seconds is not authorable; it is inert while Damage Per Tick is zero.
        [SerializeField] private DamageProfile directDamage = new DamageProfile(0f);
        [SerializeField] private BurnProfile burn = new BurnProfile(0f, 0.5f, 0f, true);

        public override TowerFamily Family => TowerFamily.Water;
        public override ElementType Element => ElementType.Water;
        public override TowerCoreProfile Core => core;
        public override ElementUpgradeCostProfile UpgradeCosts => upgradeCosts;
        public DamageProfile DirectDamage => directDamage;
        public BurnProfile Burn => burn;

        internal override void CollectSpecificValidationErrors(List<string> errors)
        {
            ElementDamageAuthoring.CollectErrors("Water", directDamage, burn, errors);
        }
    }
}
