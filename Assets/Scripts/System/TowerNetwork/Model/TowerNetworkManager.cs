using System;
using System.Collections.Generic;
using TowerDefense3D.Core;

namespace TowerDefense3D.Towers
{
    public sealed partial class TowerNetworkManager
    {
        private readonly Dictionary<TowerNodeId, NodeState> nodes = new Dictionary<TowerNodeId, NodeState>();
        private readonly List<TowerNodeId> orderedNodeIds = new List<TowerNodeId>();
        private readonly Dictionary<TowerNodeId, LinkState> outgoingLinks = new Dictionary<TowerNodeId, LinkState>();
        private readonly HashSet<TowerNodeId> nodesInValidChains = new HashSet<TowerNodeId>();
        private readonly float tickSeconds;
        private readonly float projectileSpeedMetersPerSecond;
        private readonly float maximumLinkRangeMeters;
        private readonly float maximumPushSpeedFraction;
        private readonly TowerCatalog catalog;
        private readonly StateMachine<TowerNetworkPhase> phaseMachine =
            new StateMachine<TowerNetworkPhase>(TowerNetworkPhase.Inactive, CanTransition);

        private int nextNodeId = 1;
        private int activeLevelNumber;

        public TowerNetworkManager(TowerCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            IReadOnlyList<string> errors = TowerDataValidator.CollectErrors(
                catalog, requirePlacementDefinitions: false);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException("TowerCatalog is invalid: " + string.Join(" | ", errors));
            }

            this.catalog = catalog;
            TowerCombatRules combatRules = catalog.CombatRules;
            tickSeconds = combatRules.SimulationTickSeconds;
            projectileSpeedMetersPerSecond = combatRules.ProjectileSpeedMetersPerSecond;
            maximumLinkRangeMeters = combatRules.MaximumLinkRangeMeters;
            maximumPushSpeedFraction = combatRules.MaximumPushSpeedFraction;
        }

        public event Action StateChanged;

        public int LinkCount => outgoingLinks.Count;
        public TowerCatalog Catalog => catalog;
        public int ValidChainCount { get; private set; }
        public bool HasValidChain => ValidChainCount > 0;
        public int ValidNodeCount => nodesInValidChains.Count;
        public float TickSeconds => tickSeconds;
        public float ProjectileSpeedMetersPerSecond => projectileSpeedMetersPerSecond;
        public float MaximumLinkRangeMeters => maximumLinkRangeMeters;
        public float MaximumPushSpeedFraction => maximumPushSpeedFraction;
        public bool HasLevelSession => Phase != TowerNetworkPhase.Inactive;
        public int ActiveLevelNumber => activeLevelNumber;
        public int NodeCount => nodes.Count;
        public TowerNetworkPhase Phase => phaseMachine.CurrentState;
        public bool IsRunning => Phase == TowerNetworkPhase.Running;
        public long CurrentTick { get; private set; }

        public void BeginLevelSession(int levelNumber)
        {
            if (levelNumber <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(levelNumber), "Level number must be positive.");
            }

            if (HasLevelSession)
            {
                throw new InvalidOperationException($"Tower network already owns level {activeLevelNumber}.");
            }

            ClearAllRuntimeState();
            activeLevelNumber = levelNumber;
            phaseMachine.TransitionTo(TowerNetworkPhase.Preparation);
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
            phaseMachine.TransitionTo(TowerNetworkPhase.Inactive);
            PublishStateChanged();
        }

        private void ClearAllRuntimeState()
        {
            CurrentTick = 0L;
            ClearProjectileRuntimeState();
            nodesInValidChains.Clear();
            ValidChainCount = 0;
            outgoingLinks.Clear();
            nodes.Clear();
            orderedNodeIds.Clear();
            nextNodeId = 1;
        }

        private void PublishStateChanged()
        {
            StateChanged?.Invoke();
        }

        private static bool CanTransition(
            TowerNetworkPhase currentPhase,
            TowerNetworkPhase nextPhase)
        {
            switch (currentPhase)
            {
                case TowerNetworkPhase.Inactive:
                    return nextPhase == TowerNetworkPhase.Preparation;
                case TowerNetworkPhase.Preparation:
                    return nextPhase == TowerNetworkPhase.Running
                        || nextPhase == TowerNetworkPhase.Inactive;
                case TowerNetworkPhase.Running:
                    return nextPhase == TowerNetworkPhase.Preparation
                        || nextPhase == TowerNetworkPhase.Inactive;
                default:
                    return false;
            }
        }
    }
}
