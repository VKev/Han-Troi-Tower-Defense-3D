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

        public override TowerFamily Family => TowerFamily.Water;
        public override ElementType Element => ElementType.Water;
        public override TowerCoreProfile Core => core;
        public override ElementUpgradeCostProfile UpgradeCosts => upgradeCosts;

        internal override void CollectSpecificValidationErrors(List<string> errors)
        {
        }
    }
}
