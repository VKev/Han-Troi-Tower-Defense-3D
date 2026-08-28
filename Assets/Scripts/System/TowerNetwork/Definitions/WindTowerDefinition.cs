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

        public override TowerFamily Family => TowerFamily.Wind;
        public override ElementType Element => ElementType.Wind;
        public override TowerCoreProfile Core => core;
        public override ElementUpgradeCostProfile UpgradeCosts => upgradeCosts;
        public float BasePushDistanceMeters => basePushDistanceMeters;

        internal override void CollectSpecificValidationErrors(List<string> errors)
        {
            if (basePushDistanceMeters <= 0f)
            {
                errors.Add("Wind requires a positive push distance.");
            }
        }
    }
}
