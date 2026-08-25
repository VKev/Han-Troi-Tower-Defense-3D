using System;
using TowerDefense3D.GridPlacement;
using UnityEngine;

namespace TowerDefense3D.Towers
{
    public enum TowerFamily
    {
        Generator,
        Fire,
        Water,
        Wind,
        Earth,
        SoulNexus
    }

    public enum TowerNetworkRole
    {
        Source,
        Processor,
        Sink
    }

    public enum ElementType
    {
        Fire,
        Water,
        Wind,
        Earth
    }

    [Serializable]
    public sealed class TowerNetworkProfile
    {
        [SerializeField, Min(0)] private int inputPortCount;
        [SerializeField, Min(0)] private int outputPortCount;
        [SerializeField, Min(0)] private int queueCapacityPerInput;
        [SerializeField] private bool createsBackpressure;

        public TowerNetworkProfile(
            int inputPortCount,
            int outputPortCount,
            int queueCapacityPerInput,
            bool createsBackpressure)
        {
            this.inputPortCount = inputPortCount;
            this.outputPortCount = outputPortCount;
            this.queueCapacityPerInput = queueCapacityPerInput;
            this.createsBackpressure = createsBackpressure;
        }

        public int InputPortCount => inputPortCount;
        public int OutputPortCount => outputPortCount;
        public int QueueCapacityPerInput => queueCapacityPerInput;
        public bool CreatesBackpressure => createsBackpressure;
    }

    [Serializable]
    public sealed class TowerThroughputProfile
    {
        [SerializeField, Min(0f)] private float cycleIntervalSeconds;
        [SerializeField, Min(1)] private int batchSize;
        [SerializeField, Min(1)] private int concurrentLines;
        [SerializeField, Min(0f)] private float sequenceSpacingSeconds;

        public TowerThroughputProfile(
            float cycleIntervalSeconds,
            int batchSize,
            int concurrentLines,
            float sequenceSpacingSeconds = 0f)
        {
            this.cycleIntervalSeconds = cycleIntervalSeconds;
            this.batchSize = batchSize;
            this.concurrentLines = concurrentLines;
            this.sequenceSpacingSeconds = sequenceSpacingSeconds;
        }

        public float CycleIntervalSeconds => cycleIntervalSeconds;
        public int BatchSize => batchSize;
        public int ConcurrentLines => concurrentLines;
        public float SequenceSpacingSeconds => sequenceSpacingSeconds;
        public bool IsArrivalDriven => cycleIntervalSeconds <= 0f;
    }

    [Serializable]
    public sealed class TowerEconomyProfile
    {
        [SerializeField, Min(0)] private int buildCost;
        [SerializeField, Min(0)] private int linearUpgradeCost;
        [SerializeField, Min(0)] private int maxInstancesPerLevel;
        [SerializeField] private bool sellable;

        public TowerEconomyProfile(
            int buildCost,
            int linearUpgradeCost,
            int maxInstancesPerLevel,
            bool sellable)
        {
            this.buildCost = buildCost;
            this.linearUpgradeCost = linearUpgradeCost;
            this.maxInstancesPerLevel = maxInstancesPerLevel;
            this.sellable = sellable;
        }

        public int BuildCost => buildCost;
        public int LinearUpgradeCost => linearUpgradeCost;
        public int MaxInstancesPerLevel => maxInstancesPerLevel;
        public bool Sellable => sellable;
    }

    [Serializable]
    public sealed class TowerCoreProfile
    {
        [SerializeField] private string stableId;
        [SerializeField] private string displayName;
        [SerializeField] private TowerDefinition placementDefinition;
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private TowerNetworkProfile network;
        [SerializeField] private TowerThroughputProfile throughput;
        [SerializeField] private TowerEconomyProfile economy;

        public TowerCoreProfile(
            string stableId,
            string displayName,
            TowerNetworkProfile network,
            TowerThroughputProfile throughput,
            TowerEconomyProfile economy)
        {
            this.stableId = stableId;
            this.displayName = displayName;
            this.network = network;
            this.throughput = throughput;
            this.economy = economy;
        }

        public string StableId => stableId;
        public string DisplayName => displayName;
        public TowerDefinition PlacementDefinition => placementDefinition;
        public GameObject ProjectilePrefab => projectilePrefab;
        public TowerNetworkProfile Network => network;
        public TowerThroughputProfile Throughput => throughput;
        public TowerEconomyProfile Economy => economy;
    }
}
