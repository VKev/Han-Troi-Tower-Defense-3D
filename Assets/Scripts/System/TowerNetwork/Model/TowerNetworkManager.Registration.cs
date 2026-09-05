using System;
using System.Collections.Generic;
using TowerDefense3D.Core;

namespace TowerDefense3D.Towers
{
    public sealed partial class TowerNetworkManager
    {
        public TowerNodeId RegisterTower(TowerCombatDefinition definition, TowerWorldPosition position)
        {
            RequireEditableSession();

            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            ValidatePosition(position);

            if (nextNodeId == int.MaxValue)
            {
                throw new InvalidOperationException("Tower node identifier range has been exhausted.");
            }

            TowerRuntimeSpec spec = TowerRuntimeSpecFactory.Create(definition, tickSeconds);
            TowerNodeId nodeId = new TowerNodeId(nextNodeId);
            nextNodeId++;

            nodes.Add(nodeId, new NodeState(nodeId, spec, position, definition));
            orderedNodeIds.Add(nodeId);
            RebuildValidChains();
            PublishStateChanged();
            return nodeId;
        }

        /// <summary>
        /// Raises one tower a level and rebuilds its spec so the simulation picks up the change.
        /// </summary>
        /// <remarks>
        /// Refused while a wave runs, like every other topology edit: the combat timeline is
        /// planned before the wave starts, so a tower that grew mid-wave would be simulated with
        /// numbers the plan never saw.
        /// </remarks>
        public bool TryUpgradeTower(TowerNodeId nodeId, out string error)
        {
            if (!CanEditTopology(out error))
            {
                return false;
            }

            if (!nodeId.IsValid || !nodes.TryGetValue(nodeId, out NodeState node))
            {
                error = "Tower is not registered.";
                return false;
            }

            if (node.Definition == null)
            {
                error = "Tower has no definition to upgrade from.";
                return false;
            }

            TowerUpgradeProfile upgrade = node.Definition.Core.Upgrade;
            if (!upgrade.IsUpgradable || node.UpgradeLevel >= upgrade.MaxLevel)
            {
                error = "Tower is already at its highest level.";
                return false;
            }

            node.UpgradeLevel++;
            node.Spec = TowerRuntimeSpecFactory.Create(node.Definition, tickSeconds, node.UpgradeLevel);
            PublishStateChanged();
            error = string.Empty;
            return true;
        }

        /// <summary>The tower's current level, or zero when it is unknown or never upgraded.</summary>
        public int GetUpgradeLevel(TowerNodeId nodeId)
        {
            return nodes.TryGetValue(nodeId, out NodeState node) ? node.UpgradeLevel : 0;
        }

        public bool UnregisterTower(TowerNodeId nodeId)
        {
            RequireEditableSession();

            if (!nodeId.IsValid || !nodes.ContainsKey(nodeId))
            {
                return false;
            }

            RemoveAllLinksForNode(nodeId);
            nodes.Remove(nodeId);
            orderedNodeIds.Remove(nodeId);
            RebuildValidChains();
            PublishStateChanged();
            return true;
        }

        public bool TryGetNodePosition(TowerNodeId nodeId, out TowerWorldPosition position)
        {
            if (nodes.TryGetValue(nodeId, out NodeState node))
            {
                position = node.Position;
                return true;
            }

            position = default;
            return false;
        }

        public bool TryGetNodeSpec(TowerNodeId nodeId, out TowerRuntimeSpec spec)
        {
            if (nodes.TryGetValue(nodeId, out NodeState node))
            {
                spec = node.Spec;
                return true;
            }

            spec = null;
            return false;
        }

        public bool TryCreateInputPortSnapshot(
            TowerNodeId nodeId, int inputPort, out TowerInputPortSnapshot snapshot)
        {
            if (!nodes.TryGetValue(nodeId, out NodeState node) || !node.InputBuffer.IsValidPort(inputPort))
            {
                snapshot = default;
                return false;
            }

            snapshot = new TowerInputPortSnapshot(
                nodeId, inputPort, node.InputBuffer.GetQueuedProjectileCount(inputPort),
                node.InputBuffer.GetReservedSlotCount(inputPort), node.InputBuffer.CapacityPerInput);
            return true;
        }

        public bool TryCreateQueueSummary(TowerNodeId nodeId, out TowerQueueSummary summary)
        {
            if (!nodes.TryGetValue(nodeId, out NodeState node))
            {
                summary = default;
                return false;
            }

            TowerInputBuffer inputBuffer = node.InputBuffer;
            summary = new TowerQueueSummary(
                inputBuffer.TotalQueuedProjectileCount,
                inputBuffer.TotalReservedSlotCount,
                inputBuffer.InputPortCount * inputBuffer.CapacityPerInput);
            return true;
        }

        public IReadOnlyList<TowerNodeId> CreateNodeIdSnapshot()
        {
            return orderedNodeIds.ToArray();
        }

        public IReadOnlyList<HeroAttackTowerSnapshot> CreateHeroAttackTowerSnapshot()
        {
            var snapshot = new List<HeroAttackTowerSnapshot>();
            for (int index = 0; index < orderedNodeIds.Count; index++)
            {
                NodeState node = nodes[orderedNodeIds[index]];
                if (node.Spec.Family != TowerFamily.Hero
                    || !Catalog.TryGet(node.Spec.Family, out TowerCombatDefinition definition)
                    || !(definition is HeroTowerDefinition hero))
                {
                    continue;
                }

                snapshot.Add(new HeroAttackTowerSnapshot(
                    node.Id,
                    node.Position,
                    hero.AttackRangeMeters,
                    hero.AttackDamage.Amount,
                    hero.AttackAoeRadiusMeters,
                    node.Spec.CycleTicks,
                    hero.PrepareDurationSeconds,
                    hero.LungeDurationSeconds,
                    hero.ImpactHoldDurationSeconds,
                    hero.ReturnDurationSeconds));
            }

            return snapshot;
        }

        public bool TryPeekInputProjectile(TowerNodeId nodeId, int inputPort, out ProjectileQueueEntry entry)
        {
            if (!nodes.TryGetValue(nodeId, out NodeState node) || !node.InputBuffer.IsValidPort(inputPort))
            {
                entry = default;
                return false;
            }

            return node.InputBuffer.TryPeek(inputPort, out entry);
        }

        private static void ValidatePosition(TowerWorldPosition position)
        {
            if (!FiniteNumber.IsFinite(position.X)
                || !FiniteNumber.IsFinite(position.Y)
                || !FiniteNumber.IsFinite(position.Z))
            {
                throw new ArgumentException("Tower position must contain finite coordinates.", nameof(position));
            }
        }
        private void RequireEditableSession()
        {
            if (!HasLevelSession)
            {
                throw new InvalidOperationException("No active tower-network level session.");
            }

            if (IsRunning)
            {
                throw new InvalidOperationException("Tower topology cannot change while simulation is running.");
            }
        }
    }
}
