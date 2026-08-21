using System;
using System.Collections.Generic;

namespace TowerDefense3D.Towers
{
    public sealed class TowerNetworkManager
    {
        private readonly Dictionary<TowerNodeId, NodeState> nodes = new Dictionary<TowerNodeId, NodeState>();
        private readonly List<TowerNodeId> orderedNodeIds = new List<TowerNodeId>();

        private readonly Dictionary<TowerNodeId, LinkState> outgoingLinks = new Dictionary<TowerNodeId, LinkState>();

        private readonly float tickSeconds;
        private readonly float projectileSpeedMetersPerSecond;
        private readonly float maximumLinkRangeMeters;

        private int nextNodeId = 1;
        private int activeLevelNumber;

        private readonly int minimumProcessorCountInValidChain;
        private readonly int minimumElementCountInValidChain;

        public event Action StateChanged;


        public int LinkCount => outgoingLinks.Count;

        public float TickSeconds => tickSeconds;
        public float ProjectileSpeedMetersPerSecond => projectileSpeedMetersPerSecond;
        public float MaximumLinkRangeMeters => maximumLinkRangeMeters;

        public bool HasLevelSession => activeLevelNumber > 0;
        public int ActiveLevelNumber => activeLevelNumber;
        public int NodeCount => nodes.Count;
        public bool IsRunning { get; private set; }

        public TowerNetworkManager(TowerCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            IReadOnlyList<string> errors = TowerDataValidator.CollectErrors(catalog, requirePlacementDefinitions: false);

            if (errors.Count > 0)
            {
                throw new InvalidOperationException("TowerCatalog is invalid: " + string.Join(" | ", errors));
            }

            TowerCombatRules rules = catalog.CombatRules;
            
            minimumProcessorCountInValidChain =rules.MinimumProcessorCountInValidChain;
            minimumElementCountInValidChain =rules.MinimumElementCountInValidChain;

            tickSeconds = rules.SimulationTickSeconds;
            projectileSpeedMetersPerSecond = rules.ProjectileSpeedMetersPerSecond;
            maximumLinkRangeMeters = rules.MaximumLinkRangeMeters;
        }

        public void BeginLevelSession(int levelNumber)
        {
            if (levelNumber <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(levelNumber), "Level number must be positive.");
            }

            if (HasLevelSession)
            {
                throw new InvalidOperationException($"Tower network already owns level " + $"{activeLevelNumber}.");
            }

            ClearAllRuntimeState();
            activeLevelNumber = levelNumber;
            PublishStateChanged();
        }

        public void EndLevelSession()
        {
            if (!HasLevelSession)
            {
                return;
            }

            ClearAllRuntimeState();
            activeLevelNumber = 0;
            PublishStateChanged();
        }

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

            var node = new NodeState(nodeId, spec, position);

            nodes.Add(nodeId, node);
            orderedNodeIds.Add(nodeId);

            PublishStateChanged();
            return nodeId;
        }

        public bool UnregisterTower(TowerNodeId nodeId)
        {
            RequireEditableSession();

            if (!nodeId.IsValid ||
                !nodes.ContainsKey(nodeId))
            {
                return false;
            }

            RemoveAllLinksForNode(nodeId);
            nodes.Remove(nodeId);
            orderedNodeIds.Remove(nodeId);

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

        public bool TryGetOutgoingLink(TowerNodeId sourceId, out TowerLinkSnapshot snapshot)
        {
            if (outgoingLinks.TryGetValue(
                    sourceId,
                    out LinkState link))
            {
                snapshot = new TowerLinkSnapshot(
                    link.Source,
                    link.Target,
                    link.TargetInputPort);

                return true;
            }

            snapshot = default;
            return false;
        }

        public IReadOnlyList<TowerNodeId> CreateNodeIdSnapshot()
        {
            return orderedNodeIds.ToArray();
        }
        public IReadOnlyList<TowerLinkSnapshot> CreateLinkSnapshot()
        {
            var result = new List<TowerLinkSnapshot>(outgoingLinks.Count);

            for (int index = 0;
                 index < orderedNodeIds.Count;
                 index++)
            {
                TowerNodeId sourceId = orderedNodeIds[index];

                if (!outgoingLinks.TryGetValue(sourceId, out LinkState link))
                {
                    continue;
                }

                result.Add(new TowerLinkSnapshot(link.Source, link.Target, link.TargetInputPort));
            }

            return result;
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

        private void ClearAllRuntimeState()
        {
            IsRunning = false;
            outgoingLinks.Clear();
            nodes.Clear();
            orderedNodeIds.Clear();
            nextNodeId = 1;
        }

        private void PublishStateChanged()
        {
            StateChanged?.Invoke();
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

            float distance = TowerWorldPosition.Distance(source.Position, target.Position);

            if (distance > maximumLinkRangeMeters)
            {
                error = $"Target is outside the " + $"{maximumLinkRangeMeters:0.##}m link range.";
                return false;
            }

            if (outgoingLinks.TryGetValue(sourceId, out LinkState currentLink) &&
                currentLink.Target.Equals(targetId))
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

            if (!nodeId.IsValid ||
                !nodes.ContainsKey(nodeId))
            {
                error = "Tower is not registered.";
                return false;
            }

            bool changed = RemoveAllLinksForNode(nodeId);

            if (changed)
            {
                PublishStateChanged();
            }

            error = string.Empty;
            return true;
        }

        private bool TryBuildAndCommitRewire(NodeState source, NodeState target, out string error)
        {
            var candidate = new Dictionary<TowerNodeId, LinkState>(outgoingLinks);
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
            PublishStateChanged();

            error = string.Empty;
            return true;
        }

        private static int PrepareSingleInputTarget(IDictionary<TowerNodeId, LinkState> candidate, TowerNodeId targetId)
        {
            if (TryFindIncomingSource(
                    candidate,
                    targetId,
                    targetInputPort: 0,
                    out TowerNodeId displacedSource))
            {
                candidate.Remove(displacedSource);
            }

            return 0;
        }

        private static int FindFirstFreeInputPort(
            IDictionary<TowerNodeId, LinkState> links,
            TowerNodeId targetId,
            int inputPortCount)
        {
            for (int port = 0; port < inputPortCount; port++)
            {
                if (!TryFindIncomingSource(links, targetId, port, out _))
                {
                    return port;
                }
            }

            return -1;
        }

        private bool ContainsCycle(IReadOnlyDictionary<TowerNodeId, LinkState> links)
        {
            for (int index = 0; index < orderedNodeIds.Count; index++)
            {
                TowerNodeId current = orderedNodeIds[index];
                var currentPath = new HashSet<TowerNodeId>();

                while (links.TryGetValue(current, out LinkState link))
                {
                    if (!currentPath.Add(current))
                    {
                        return true;
                    }

                    current = link.Target;
                }
            }

            return false;
        }

        private bool RemoveAllLinksForNode(TowerNodeId nodeId)
        {
            bool changed = outgoingLinks.Remove(nodeId);
            var incomingSources = new List<TowerNodeId>();

            foreach (KeyValuePair<TowerNodeId, LinkState> pair in outgoingLinks)
            {
                if (pair.Value.Target.Equals(nodeId))
                {
                    incomingSources.Add(pair.Key);
                }
            }

            for (int index = 0; index < incomingSources.Count; index++)
            {
                changed |= outgoingLinks.Remove(incomingSources[index]);
            }

            return changed;
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
            IDictionary<TowerNodeId, LinkState> links,
            TowerNodeId targetId,
            int targetInputPort,
            out TowerNodeId sourceId)
        {
            foreach (KeyValuePair<TowerNodeId, LinkState> pair in links)
            {
                LinkState link = pair.Value;

                if (link.Target.Equals(targetId) &&
                    link.TargetInputPort == targetInputPort)
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
                error =
                    "No active tower-network level session.";
                return false;
            }

            if (IsRunning)
            {
                error =
                    "Tower topology cannot change while " +
                    "simulation is running.";
                return false;
            }

            error = string.Empty;
            return true;
        }


        private sealed class NodeState
        {
            public NodeState(
                TowerNodeId id,
                TowerRuntimeSpec spec,
                TowerWorldPosition position)
            {
                Id = id;
                Spec = spec ??
                       throw new ArgumentNullException(nameof(spec));
                Position = position;
            }

            public TowerNodeId Id { get; }
            public TowerRuntimeSpec Spec { get; }
            public TowerWorldPosition Position { get; }
        }

        private sealed class LinkState
        {
            public LinkState(
                TowerNodeId source,
                TowerNodeId target,
                int targetInputPort)
            {
                Source = source;
                Target = target;
                TargetInputPort = targetInputPort;
            }

            public TowerNodeId Source { get; }
            public TowerNodeId Target { get; }
            public int TargetInputPort { get; }
        }
    }


}