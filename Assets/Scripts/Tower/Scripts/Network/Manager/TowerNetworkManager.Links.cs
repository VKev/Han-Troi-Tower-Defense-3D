using System.Collections.Generic;

namespace TowerDefense3D.Towers
{
    public sealed partial class TowerNetworkManager
    {
        public bool TryGetOutgoingLink(TowerNodeId sourceId, out TowerLinkSnapshot snapshot)
        {
            if (outgoingLinks.TryGetValue(sourceId, out LinkState link))
            {
                snapshot = new TowerLinkSnapshot(link.Source, link.Target, link.TargetInputPort);
                return true;
            }

            snapshot = default;
            return false;
        }

        public IReadOnlyList<TowerLinkSnapshot> CreateLinkSnapshot()
        {
            List<TowerLinkSnapshot> snapshot = new List<TowerLinkSnapshot>(outgoingLinks.Count);

            for (int index = 0; index < orderedNodeIds.Count; index++)
            {
                TowerNodeId sourceId = orderedNodeIds[index];
                if (outgoingLinks.TryGetValue(sourceId, out LinkState link))
                {
                    snapshot.Add(new TowerLinkSnapshot(link.Source, link.Target, link.TargetInputPort));
                }
            }

            return snapshot;
        }

        public bool TryRewire(TowerNodeId sourceId, TowerNodeId targetId, out string error)
        {
            if (!CanEditTopology(out error))
            {
                return false;
            }

            if (!nodes.TryGetValue(sourceId, out NodeState source))
            {
                error = "Source tower is not registered.";
                return false;
            }

            if (!nodes.TryGetValue(targetId, out NodeState target))
            {
                error = "Target tower is not registered.";
                return false;
            }

            if (sourceId.Equals(targetId))
            {
                error = "A tower cannot link to itself.";
                return false;
            }

            if (source.Spec.OutputPortCount <= 0)
            {
                error = "The selected source tower has no output port.";
                return false;
            }

            if (target.Spec.InputPortCount <= 0)
            {
                error = "The selected target tower has no input port.";
                return false;
            }

            if (TowerWorldPosition.Distance(source.Position, target.Position) > maximumLinkRangeMeters)
            {
                error = $"Target is outside the {maximumLinkRangeMeters:0.##}m link range.";
                return false;
            }

            if (outgoingLinks.TryGetValue(sourceId, out LinkState oldLink) && oldLink.Target.Equals(targetId))
            {
                error = string.Empty;
                return true;
            }

            return TryBuildAndCommitRewire(source, target, out error);
        }

        public bool TryUnlinkAll(TowerNodeId nodeId, out string error)
        {
            if (!CanEditTopology(out error))
            {
                return false;
            }

            if (!nodeId.IsValid || !nodes.ContainsKey(nodeId))
            {
                error = "Tower is not registered.";
                return false;
            }

            if (RemoveAllLinksForNode(nodeId))
            {
                RebuildValidChains();
                PublishStateChanged();
            }

            error = string.Empty;
            return true;
        }

        private bool TryBuildAndCommitRewire(NodeState source, NodeState target, out string error)
        {
            Dictionary<TowerNodeId, LinkState> candidate = new Dictionary<TowerNodeId, LinkState>(outgoingLinks);
            candidate.Remove(source.Id);

            int targetInputPort;
            if (target.Spec.InputPortCount == 1)
            {
                targetInputPort = PrepareSingleInputTarget(candidate, target.Id);
            }
            else
            {
                targetInputPort = FindFirstFreeInputPort(candidate, target.Id, target.Spec.InputPortCount);
                if (targetInputPort < 0)
                {
                    error = "Every target input port is occupied.";
                    return false;
                }
            }

            candidate[source.Id] = new LinkState(source.Id, target.Id, targetInputPort);
            if (ContainsCycle(candidate))
            {
                error = "The requested link would create a cycle.";
                return false;
            }

            CommitLinks(candidate);
            RebuildValidChains();
            PublishStateChanged();
            error = string.Empty;
            return true;
        }

        private static int PrepareSingleInputTarget(IDictionary<TowerNodeId, LinkState> candidate, TowerNodeId targetId)
        {
            if (TryFindIncomingSource(candidate, targetId, 0, out TowerNodeId sourceId))
            {
                candidate.Remove(sourceId);
            }

            return 0;
        }

        private static int FindFirstFreeInputPort(
            IDictionary<TowerNodeId, LinkState> links, TowerNodeId targetId, int inputPortCount)
        {
            for (int inputPort = 0; inputPort < inputPortCount; inputPort++)
            {
                if (!TryFindIncomingSource(links, targetId, inputPort, out _))
                {
                    return inputPort;
                }
            }

            return -1;
        }

        private bool ContainsCycle(IReadOnlyDictionary<TowerNodeId, LinkState> links)
        {
            for (int index = 0; index < orderedNodeIds.Count; index++)
            {
                TowerNodeId currentId = orderedNodeIds[index];
                HashSet<TowerNodeId> visited = new HashSet<TowerNodeId>();

                while (links.TryGetValue(currentId, out LinkState link))
                {
                    if (!visited.Add(currentId))
                    {
                        return true;
                    }

                    currentId = link.Target;
                }
            }

            return false;
        }

        private bool RemoveAllLinksForNode(TowerNodeId nodeId)
        {
            bool removed = outgoingLinks.Remove(nodeId);
            List<TowerNodeId> incomingSources = new List<TowerNodeId>();

            foreach (KeyValuePair<TowerNodeId, LinkState> pair in outgoingLinks)
            {
                if (pair.Value.Target.Equals(nodeId))
                {
                    incomingSources.Add(pair.Key);
                }
            }

            for (int index = 0; index < incomingSources.Count; index++)
            {
                removed |= outgoingLinks.Remove(incomingSources[index]);
            }

            return removed;
        }

        private void CommitLinks(IReadOnlyDictionary<TowerNodeId, LinkState> candidate)
        {
            outgoingLinks.Clear();
            foreach (KeyValuePair<TowerNodeId, LinkState> pair in candidate)
            {
                outgoingLinks.Add(pair.Key, pair.Value);
            }
        }

        private static bool TryFindIncomingSource(
            IDictionary<TowerNodeId, LinkState> links, TowerNodeId targetId, int targetInputPort,
            out TowerNodeId sourceId)
        {
            foreach (KeyValuePair<TowerNodeId, LinkState> pair in links)
            {
                LinkState link = pair.Value;
                if (link.Target.Equals(targetId) && link.TargetInputPort == targetInputPort)
                {
                    sourceId = pair.Key;
                    return true;
                }
            }

            sourceId = default;
            return false;
        }

        private bool CanEditTopology(out string error)
        {
            if (!HasLevelSession)
            {
                error = "No active tower-network level session.";
                return false;
            }

            if (IsRunning)
            {
                error = "Tower topology cannot change while simulation is running.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
