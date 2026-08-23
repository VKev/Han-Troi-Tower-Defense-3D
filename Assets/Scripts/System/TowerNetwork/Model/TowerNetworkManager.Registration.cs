using System;
using System.Collections.Generic;

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

            nodes.Add(nodeId, new NodeState(nodeId, spec, position));
            orderedNodeIds.Add(nodeId);
            RebuildValidChains();
            PublishStateChanged();
            return nodeId;
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
            if (!IsFinite(position.X) || !IsFinite(position.Y) || !IsFinite(position.Z))
            {
                throw new ArgumentException("Tower position must contain finite coordinates.", nameof(position));
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
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
