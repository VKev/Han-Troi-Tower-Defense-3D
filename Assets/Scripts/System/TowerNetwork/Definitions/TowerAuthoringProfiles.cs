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
        SoulNexus,
        Hero
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
        Wind
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

    /// <summary>
    /// What one upgrade of this tower costs and what it buys.
    /// </summary>
    /// <remarks>
    /// Authored per tower rather than derived, so balancing an upgrade is a data edit. Cost is
    /// linear in the level being bought - the first upgrade costs one step, the second two - which
    /// keeps a late upgrade meaningfully more expensive without a per-level cost table.
    ///
    /// For now the only thing a level buys is damage. That is deliberate: it is the one stat the
    /// combat timeline reads straight off the tower's payload, so it can be raised without the
    /// simulation having to learn about levels at all.
    /// </remarks>
    [Serializable]
    public sealed class TowerUpgradeProfile
    {
        [SerializeField, Min(0)] private int maxLevel = 3;
        [SerializeField, Min(0)] private int costPerLevel = 150;
        [SerializeField, Min(0f)] private float damageBonusPerLevel = 0.35f;

        public TowerUpgradeProfile()
        {
        }

        public TowerUpgradeProfile(int maxLevel, int costPerLevel, float damageBonusPerLevel)
        {
            this.maxLevel = maxLevel;
            this.costPerLevel = costPerLevel;
            this.damageBonusPerLevel = damageBonusPerLevel;
        }

        public int MaxLevel => maxLevel;
        public int CostPerLevel => costPerLevel;
        public float DamageBonusPerLevel => damageBonusPerLevel;

        /// <summary>Whether this tower can be upgraded at all.</summary>
        public bool IsUpgradable => maxLevel > 0 && costPerLevel >= 0;

        /// <summary>What it costs to go from <paramref name="currentLevel"/> to the next one.</summary>
        public int CostToReach(int currentLevel)
        {
            return costPerLevel * (currentLevel + 1);
        }

        /// <summary>Damage scale at a given level. Level zero is the authored damage, untouched.</summary>
        public float DamageMultiplier(int level)
        {
            return level <= 0 ? 1f : 1f + damageBonusPerLevel * level;
        }
    }

    [Serializable]
    public sealed class TowerCoreProfile
    {
        [SerializeField] private string stableId;
        [SerializeField] private string displayName;
        [SerializeField] private TowerDefinition placementDefinition;
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private GameObject hitEffectPrefab;
        [SerializeField] private TowerNetworkProfile network;
        [SerializeField] private TowerThroughputProfile throughput;
        [SerializeField] private TowerEconomyProfile economy;
        [SerializeField] private TowerUpgradeProfile upgrade = new TowerUpgradeProfile();

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
        public GameObject HitEffectPrefab => hitEffectPrefab;
        public TowerNetworkProfile Network => network;
        public TowerThroughputProfile Throughput => throughput;
        public TowerEconomyProfile Economy => economy;

        /// <summary>Never null: an asset authored before upgrades existed still answers with defaults.</summary>
        public TowerUpgradeProfile Upgrade => upgrade ?? (upgrade = new TowerUpgradeProfile());
    }
}
