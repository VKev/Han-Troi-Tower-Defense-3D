using System;
using TowerDefense3D.Enemies;
using UnityEngine;

namespace TowerDefense3D.Towers
{
    public sealed class HeroAttackPresentationSystem : IDisposable
    {
        private readonly TowerNetworkSystem towerNetworkSystem;
        private readonly CombatTimelineSystem combatTimelineSystem;
        private bool isStarted;

        public HeroAttackPresentationSystem(
            TowerNetworkSystem towerNetworkSystem,
            CombatTimelineSystem combatTimelineSystem)
        {
            this.towerNetworkSystem = towerNetworkSystem
                ?? throw new ArgumentNullException(nameof(towerNetworkSystem));
            this.combatTimelineSystem = combatTimelineSystem
                ?? throw new ArgumentNullException(nameof(combatTimelineSystem));
        }

        public void Start()
        {
            combatTimelineSystem.HeroAttackStarted += HandleHeroAttackStarted;
            isStarted = true;
        }

        public void Dispose()
        {
            if (!isStarted)
            {
                return;
            }

            isStarted = false;
            combatTimelineSystem.HeroAttackStarted -= HandleHeroAttackStarted;
        }

        private void HandleHeroAttackStarted(HeroAttackEvent attack)
        {
            if (!towerNetworkSystem.TryGetTowerView(attack.TowerNodeId, out ITowerRuntimeView tower))
            {
                return;
            }

            MonoBehaviour[] components = tower.GameObject.GetComponentsInChildren<MonoBehaviour>(true);
            for (int index = 0; index < components.Length; index++)
            {
                if (components[index] is IHeroAttackView attackView)
                {
                    attackView.PlayAttack(attack);
                    return;
                }
            }
        }
    }
}
