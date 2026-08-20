using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.Towers
{
    public abstract class TowerCombatDefinition : ScriptableObject
    {
        public abstract TowerFamily Family { get; }
        public abstract TowerNetworkRole NetworkRole { get; }
        public abstract TowerCoreProfile Core { get; }

        internal abstract void CollectSpecificValidationErrors(List<string> errors);
    }

    public abstract class ElementTowerDefinition : TowerCombatDefinition
    {
        public sealed override TowerNetworkRole NetworkRole => TowerNetworkRole.Processor;
        public abstract ElementType Element { get; }
        public abstract ElementUpgradeCostProfile UpgradeCosts { get; }
    }
}
