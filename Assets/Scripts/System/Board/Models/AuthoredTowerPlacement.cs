using System;
using TowerDefense3D.Towers;
using UnityEngine;

namespace TowerDefense3D.GridPlacement
{
    [Serializable]
    public struct AuthoredTowerPlacement
    {
        [SerializeField] private GridCell coordinate;
        [SerializeField] private TowerCombatDefinition definition;

        public AuthoredTowerPlacement(GridCell coordinate, TowerCombatDefinition definition)
        {
            this.coordinate = coordinate;
            this.definition = definition;
        }

        public GridCell Coordinate => coordinate;
        public TowerCombatDefinition Definition => definition;
    }
}
