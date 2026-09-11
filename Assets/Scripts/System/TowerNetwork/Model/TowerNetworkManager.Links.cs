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
            if (!TryOpenLinkGate(sourceId, targetId, out NodeState source, out NodeState target, out error))
            {
                return false;
            }

            if (outgoingLinks.TryGetValue(sourceId, out LinkState oldLink) && oldLink.Target.Equals(targetId))
            {
                error = string.Empty;
                return true;
            }

            return TryBuildAndCommitRewire(source, target, out error);
        }

        /// <summary>
        /// Whether a link from <paramref name="sourceId"/> to <paramref name="targetId"/> would be
        /// accepted, asked without building anything.
        /// </summary>
        /// <remarks>
        /// The drag preview asks this every frame so the line under the finger already carries the
        /// answer the release will give. It reads the same gate <see cref="TryRewire"/> reads, so
        /// the preview cannot draw a link that the release then refuses: a second, parallel copy of
        /// the distance rule is exactly how a green line that will not attach comes about.
        ///
        /// What it does not run is the rewire itself, which reshuffles input ports and re-derives
        /// the chains. That work allocates, and this is asked once a frame while a finger is
        /// moving. A link this accepts can still fail there under port pressure - but never on
        /// distance, which is the answer the player is being shown.
        /// </remarks>
        public bool CanLink(TowerNodeId sourceId, TowerNodeId targetId)
        {
            return TryOpenLinkGate(sourceId, targetId, out _, out _, out _);
        }

        /// <summary>
        /// Whether a link may be started from <paramref name="sourceId"/> at all.
        /// </summary>
        /// <remarks>
        /// Asked before the gesture begins, not when it ends. A drag that can never attach still
        /// draws a line across the board and still reads as an offer; refusing it on release only
        /// tells the player afterwards. This covers the reasons that are already settled before a
        /// target is known - the wave has started, or the tower is a hero - and leaves the ones
        /// that depend on the target to <see cref="CanLink"/>.
        /// </remarks>
        public bool CanStartLink(TowerNodeId sourceId)
        {
            return CanEditTopology(out _)
                && nodes.TryGetValue(sourceId, out NodeState source)
                && source.Spec.Family != TowerFamily.Hero
                && source.Spec.OutputPortCount > 0;
        }

        /// <summary>
        /// Every reason a link can be turned away before the topology is touched, in one place so
        /// asking and doing cannot answer differently.
        /// </summary>
        private bool TryOpenLinkGate(
            TowerNodeId sourceId,
            TowerNodeId targetId,
            out NodeState source,
            out NodeState target,
            out string error)
        {
            source = null;
            target = null;
            if (!CanEditTopology(out error))
            {
                return false;
            }

            if (!nodes.TryGetValue(sourceId, out source))
            {
                error = "Trụ nguồn chưa được đăng ký.";
                return false;
            }

            if (!nodes.TryGetValue(targetId, out target))
            {
                error = "Trụ đích chưa được đăng ký.";
                return false;
            }

            if (sourceId.Equals(targetId))
            {
                error = "Không thể nối trụ với chính nó.";
                return false;
            }

            // A hero fights on its own: it strikes what comes into its own reach and neither
            // feeds nor is fed by the network. Its port counts say otherwise only because every
            // tower is validated against one shape, so the rule lives here where it is read.
            if (source.Spec.Family == TowerFamily.Hero || target.Spec.Family == TowerFamily.Hero)
            {
                error = "Anh hùng tự tấn công và không thể nối link.";
                return false;
            }

            if (source.Spec.OutputPortCount <= 0)
            {
                error = "Trụ nguồn không có cổng đầu ra.";
                return false;
            }

            if (target.Spec.InputPortCount <= 0)
            {
                error = "Trụ đích không có cổng đầu vào.";
                return false;
            }

            if (TowerWorldPosition.Distance(source.Position, target.Position) > source.Spec.RangeMeters)
            {
                error = $"Trụ đích nằm ngoài tầm nối {source.Spec.RangeMeters:0.##}m.";
                return false;
            }

            error = string.Empty;
            return true;
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
            if (!TryBuildRewireCandidate(source, target, out Dictionary<TowerNodeId, LinkState> candidate, out error))
            {
                return false;
            }

            CommitLinks(candidate);
            RebuildValidChains();
            PublishStateChanged();
            return true;
        }

        private bool TryBuildRewireCandidate(NodeState source, NodeState target,
            out Dictionary<TowerNodeId, LinkState> candidate, out string error)
        {
            candidate = new Dictionary<TowerNodeId, LinkState>(outgoingLinks);
            candidate.Remove(source.Id);

            int targetInputPort = FindFirstFreeInputPort(candidate, target.Id, target.Spec.InputPortCount);
            if (targetInputPort < 0)
            {
                // Every input is taken, so the new link displaces the one holding the first port
                // instead of being turned away. The drag preview cannot see port pressure, so
                // refusing here would reject a line the player was already shown as attachable.
                targetInputPort = 0;
                if (TryFindIncomingSource(candidate, target.Id, targetInputPort, out TowerNodeId displaced))
                {
                    candidate.Remove(displaced);
                }
            }

            candidate[source.Id] = new LinkState(source.Id, target.Id, targetInputPort);
            if (ContainsCycle(candidate))
            {
                    error = "Link này sẽ tạo thành vòng lặp.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryCollectPreviewValidLinks(TowerNodeId sourceId, TowerNodeId targetId,
            IDictionary<TowerNodeId, TowerNodeId> validLinks)
        {
            validLinks.Clear();
            if (!TryOpenLinkGate(sourceId, targetId, out NodeState source, out NodeState target, out _))
            {
                return false;
            }

            IReadOnlyDictionary<TowerNodeId, LinkState> candidate = outgoingLinks;
            if (!outgoingLinks.TryGetValue(sourceId, out LinkState existing) || !existing.Target.Equals(targetId))
            {
                if (!TryBuildRewireCandidate(source, target, out Dictionary<TowerNodeId, LinkState> proposed, out _))
                {
                    return false;
                }

                candidate = proposed;
            }

            var route = new List<TowerNodeId>();
            foreach (TowerNodeId nodeId in orderedNodeIds)
            {
                if (!TryCollectValidRoute(nodeId, route, candidate))
                {
                    continue;
                }

                for (int index = 0; index < route.Count - 1; index++)
                {
                    validLinks[route[index]] = route[index + 1];
                }
            }

            return true;
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
                error = "Chưa có phiên bản đồ đang hoạt động.";
                return false;
            }

            if (IsRunning)
            {
                error = "Không thể thay đổi link khi mô phỏng đang chạy.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
