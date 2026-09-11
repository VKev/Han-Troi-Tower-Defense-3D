using System;
using UnityEngine;

namespace TowerDefense3D.Towers
{
    /// <summary>
    /// Shows the model authored for a tower's new tier when it is upgraded.
    /// </summary>
    public sealed class TowerTierVisualPresentationSystem : IDisposable
    {
        private readonly TowerNetworkSystem towerNetworkSystem;

        private bool isStarted;

        public TowerTierVisualPresentationSystem(TowerNetworkSystem towerNetworkSystem)
        {
            this.towerNetworkSystem = towerNetworkSystem
                ?? throw new ArgumentNullException(nameof(towerNetworkSystem));
        }

        public void Start()
        {
            towerNetworkSystem.TowerUpgraded += HandleTowerUpgraded;
            isStarted = true;
        }

        public void Dispose()
        {
            if (!isStarted)
            {
                return;
            }

            towerNetworkSystem.TowerUpgraded -= HandleTowerUpgraded;
            isStarted = false;
        }

        private void HandleTowerUpgraded(ITowerRuntimeView tower)
        {
            if (tower == null)
            {
                return;
            }

            // A tier with no model authored is a stats-only upgrade: the tower keeps the model it
            // already has rather than being swapped for nothing.
            GameObject visualPrefab = tower.CombatDefinition
                ?.UpgradeCosts
                ?.GetTier(towerNetworkSystem.GetUpgradeLevel(tower))
                ?.VisualPrefab;
            if (visualPrefab == null)
            {
                return;
            }

            tower.ReplaceVisual(visualPrefab);
        }
    }
}
