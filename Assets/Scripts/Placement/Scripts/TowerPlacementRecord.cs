using System;
using TowerDefense3D.Towers;
using UnityEngine;

namespace TowerDefense3D.GridPlacement
{
    public readonly struct TowerPlacementRecord
    {
        public TowerPlacementRecord(
            TowerCombatDefinition combatDefinition,
            TowerDefinition placementDefinition,
            TowerRuntimeView runtimeView,
            GridCell anchor,
            int ownerId)
        {
            CombatDefinition = combatDefinition != null
                ? combatDefinition
                : throw new ArgumentNullException(nameof(combatDefinition));
            PlacementDefinition = placementDefinition != null
                ? placementDefinition
                : throw new ArgumentNullException(nameof(placementDefinition));
            RuntimeView = runtimeView != null
                ? runtimeView
                : throw new ArgumentNullException(nameof(runtimeView));

            if (ownerId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ownerId), "Placement owner ID must be positive.");
            }

            Anchor = anchor;
            OwnerId = ownerId;
        }

        public TowerCombatDefinition CombatDefinition { get; }
        public TowerDefinition PlacementDefinition { get; }
        public TowerRuntimeView RuntimeView { get; }
        public GridCell Anchor { get; }
        public int OwnerId { get; }
    }
}
