using System;
using System.Collections.Generic;
using TowerDefense3D.Enemies;
using UnityEngine;

namespace TowerDefense3D.Towers
{
    public sealed class HeroAttackPresentationSystem : IDisposable
    {
        private readonly TowerNetworkSystem towerNetworkSystem;
        private readonly CombatTimelineSystem combatTimelineSystem;
        private readonly Dictionary<TowerNodeId, IHeroAttackView> attackViews = new();
        private bool isStarted;
        private float animationSpeed = 1f;

        public event Action HeroHitEffectPlayed;

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
            towerNetworkSystem.StateChanged += RefreshAttackViews;
            RefreshAttackViews();
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
            towerNetworkSystem.StateChanged -= RefreshAttackViews;
            ClearAttackViews();
        }

        public void LateTick(float speed)
        {
            animationSpeed = speed;
            foreach (IHeroAttackView attackView in attackViews.Values)
            {
                attackView.SetAnimationSpeed(speed);
            }
        }

        private void HandleHeroAttackStarted(HeroAttackEvent attack)
        {
            if (attackViews.TryGetValue(attack.TowerNodeId, out IHeroAttackView cachedAttackView))
            {
                cachedAttackView.SetAnimationSpeed(animationSpeed);
                cachedAttackView.PlayAttack(attack);
                return;
            }

            if (!towerNetworkSystem.TryGetTowerView(attack.TowerNodeId, out ITowerRuntimeView tower))
            {
                return;
            }

            MonoBehaviour[] components = tower.GameObject.GetComponentsInChildren<MonoBehaviour>(true);
            for (int index = 0; index < components.Length; index++)
            {
                if (components[index] is IHeroAttackView attackView)
                {
                    CacheAttackView(attack.TowerNodeId, attackView);
                    attackView.PlayAttack(attack);
                    return;
                }
            }
        }

        private void RefreshAttackViews()
        {
            ClearAttackViews();
            IReadOnlyList<ITowerRuntimeView> towers = towerNetworkSystem.CreateTowerViewSnapshot();
            for (int towerIndex = 0; towerIndex < towers.Count; towerIndex++)
            {
                ITowerRuntimeView tower = towers[towerIndex];
                if (!(tower.CombatDefinition is HeroTowerDefinition) || tower.GameObject == null)
                {
                    continue;
                }

                MonoBehaviour[] components = tower.GameObject.GetComponentsInChildren<MonoBehaviour>(true);
                for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
                {
                    if (components[componentIndex] is IHeroAttackView attackView)
                    {
                        CacheAttackView(tower.NodeId, attackView);
                        break;
                    }
                }
            }
        }

        private void CacheAttackView(TowerNodeId nodeId, IHeroAttackView attackView)
        {
            if (attackViews.TryGetValue(nodeId, out IHeroAttackView existing))
            {
                existing.HitEffectPlayed -= HandleHeroHitEffectPlayed;
            }

            attackViews[nodeId] = attackView;
            attackView.HitEffectPlayed += HandleHeroHitEffectPlayed;
            attackView.SetAnimationSpeed(animationSpeed);
        }

        private void ClearAttackViews()
        {
            foreach (IHeroAttackView attackView in attackViews.Values)
            {
                attackView.HitEffectPlayed -= HandleHeroHitEffectPlayed;
            }

            attackViews.Clear();
        }

        private void HandleHeroHitEffectPlayed()
        {
            HeroHitEffectPlayed?.Invoke();
        }
    }
}
