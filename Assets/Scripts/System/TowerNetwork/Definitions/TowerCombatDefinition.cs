using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.Towers
{
    public abstract class TowerCombatDefinition : ScriptableObject
    {
        [SerializeField] private TowerUpgradeCostProfile upgradeCosts =
            new TowerUpgradeCostProfile();

        public abstract TowerFamily Family { get; }
        public abstract TowerNetworkRole NetworkRole { get; }
        public abstract TowerCoreProfile Core { get; }
        public TowerUpgradeCostProfile UpgradeCosts => upgradeCosts
            ?? (upgradeCosts = new TowerUpgradeCostProfile());

        /// <summary>
        /// Level the player has to clear before this tower may be built. Zero, the default,
        /// leaves the tower buildable from the first level.
        /// </summary>
        public virtual int UnlockAfterClearingLevelNumber => 0;

        internal abstract void CollectSpecificValidationErrors(List<string> errors);
    }

    public abstract class ElementTowerDefinition : TowerCombatDefinition
    {
        public sealed override TowerNetworkRole NetworkRole => TowerNetworkRole.Processor;
        public abstract ElementType Element { get; }
    }
}
