using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.Towers
{
    [CreateAssetMenu(
        fileName = "WindTower",
        menuName = "Tower Defense/Towers/Elements/Wind")]
    public sealed class WindTowerDefinition : ElementTowerDefinition
    {
        [SerializeField] private TowerCoreProfile core = new TowerCoreProfile(
            "wind",
            "Wind",
            new TowerNetworkProfile(1, 1, 3, true),
            new TowerThroughputProfile(0.85f, 1, 1),
            new TowerEconomyProfile(70, 0, 0, true));
        [SerializeField] private ElementUpgradeCostProfile upgradeCosts =
            new ElementUpgradeCostProfile();
        [SerializeField, Min(0.01f)] private float basePushDistanceMeters = 0.5f;

        // Authored at zero on purpose: Wind's identity is the push, not damage. The knobs
        // exist so the Game Balance Center can tune it without a code change, and zero here
        // reads as "no damage yet" rather than "this tower cannot deal damage". The tick
        // interval sits at its field minimum because a burn that ticks every zero seconds is
        // not authorable; it is inert while Damage Per Tick is zero.
        [SerializeField] private DamageProfile directDamage = new DamageProfile(0f);
        [SerializeField] private BurnProfile burn = new BurnProfile(0f, 0.5f, 0f, true);

        public override TowerFamily Family => TowerFamily.Wind;
        public override ElementType Element => ElementType.Wind;
        public override TowerCoreProfile Core => core;
        public override ElementUpgradeCostProfile UpgradeCosts => upgradeCosts;
        public float BasePushDistanceMeters => basePushDistanceMeters;
        public DamageProfile DirectDamage => directDamage;
        public BurnProfile Burn => burn;

        internal override void CollectSpecificValidationErrors(List<string> errors)
        {
            if (basePushDistanceMeters <= 0f)
            {
                errors.Add("Wind requires a positive push distance.");
            }

            ElementDamageAuthoring.CollectErrors("Wind", directDamage, burn, errors);
        }
    }
}
