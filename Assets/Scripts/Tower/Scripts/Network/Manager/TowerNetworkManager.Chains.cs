using System.Collections.Generic;

namespace TowerDefense3D.Towers
{
    public sealed partial class TowerNetworkManager
    {
        public bool IsNodeInValidChain(TowerNodeId nodeId)
        {
            return nodesInValidChains.Contains(nodeId);
        }

        public IReadOnlyList<TowerNodeId> CreateValidNodeIdSnapshot()
        {
            List<TowerNodeId> snapshot = new List<TowerNodeId>(nodesInValidChains.Count);

            for (int index = 0; index < orderedNodeIds.Count; index++)
            {
                TowerNodeId nodeId = orderedNodeIds[index];
                if (nodesInValidChains.Contains(nodeId))
                {
                    snapshot.Add(nodeId);
                }
            }

            return snapshot;
        }

        private static bool IsGeneratorSource(NodeState node)
        {
            return node.Spec.NetworkRole == TowerNetworkRole.Source && node.Spec.Family == TowerFamily.Generator;
        }

        private static bool IsSoulNexusSink(NodeState node)
        {
            return node.Spec.NetworkRole == TowerNetworkRole.Sink && node.Spec.Family == TowerFamily.SoulNexus;
        }

        private bool TryCollectValidRoute(TowerNodeId startId, List<TowerNodeId> route)
        {
            route.Clear();

            if (!nodes.TryGetValue(startId, out NodeState start) || !IsGeneratorSource(start))
            {
                return false;
            }

            HashSet<TowerNodeId> visited = new HashSet<TowerNodeId>();
            TowerNodeId currentId = startId;

            while (nodes.TryGetValue(currentId, out NodeState current))
            {
                if (!visited.Add(currentId))
                {
                    return false;
                }

                route.Add(currentId);
                if (current.Spec.NetworkRole == TowerNetworkRole.Sink)
                {
                    return IsSoulNexusSink(current);
                }

                if (!currentId.Equals(startId) && current.Spec.NetworkRole != TowerNetworkRole.Processor)
                {
                    return false;
                }

                if (!outgoingLinks.TryGetValue(currentId, out LinkState link))
                {
                    return false;
                }

                currentId = link.Target;
            }

            return false;
        }

        private void RebuildValidChains()
        {
            nodesInValidChains.Clear();
            ValidChainCount = 0;
            List<TowerNodeId> route = new List<TowerNodeId>();

            for (int index = 0; index < orderedNodeIds.Count; index++)
            {
                TowerNodeId nodeId = orderedNodeIds[index];
                if (!nodes.TryGetValue(nodeId, out NodeState node) || !IsGeneratorSource(node) ||
                    !TryCollectValidRoute(nodeId, route))
                {
                    continue;
                }

                ValidChainCount++;
                for (int routeIndex = 0; routeIndex < route.Count; routeIndex++)
                {
                    nodesInValidChains.Add(route[routeIndex]);
                }
            }
        }
    }
}
