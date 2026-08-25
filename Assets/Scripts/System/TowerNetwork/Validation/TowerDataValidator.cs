using System;
using System.Collections.Generic;

namespace TowerDefense3D.Towers
{
    public static class TowerDataValidator
    {
        public static IReadOnlyList<string> CollectErrors(
            TowerCombatDefinition definition,
            bool requirePlacementDefinition = true)
        {
            var errors = new List<string>();
            if (definition == null)
            {
                errors.Add("Tower definition is missing.");
                return errors;
            }

            TowerCoreProfile core = definition.Core;
            if (core == null)
            {
                errors.Add($"{definition.name}: Core profile is missing.");
                return errors;
            }

            if (string.IsNullOrWhiteSpace(core.StableId))
            {
                errors.Add($"{definition.name}: Stable Id is required.");
            }

            if (string.IsNullOrWhiteSpace(core.DisplayName))
            {
                errors.Add($"{definition.name}: Display Name is required.");
            }

            if (requirePlacementDefinition && core.PlacementDefinition == null)
            {
                errors.Add($"{core.StableId}: Placement Definition is required.");
            }

            if (requirePlacementDefinition &&
                definition.NetworkRole != TowerNetworkRole.Sink &&
                core.ProjectilePrefab == null)
            {
                errors.Add($"{core.StableId}: Projectile Prefab is required.");
            }

            if (requirePlacementDefinition &&
                definition.NetworkRole == TowerNetworkRole.Sink &&
                core.ProjectilePrefab != null)
            {
                errors.Add($"{core.StableId}: Sink towers cannot author a Projectile Prefab.");
            }

            ValidateNetwork(definition, core.Network, errors);
            ValidateThroughput(definition, core.Throughput, errors);
            ValidateEconomy(core.Economy, errors);
            definition.CollectSpecificValidationErrors(errors);
            return errors;
        }

        public static IReadOnlyList<string> CollectErrors(TowerCombatRules rules)
        {
            var errors = new List<string>();
            if (rules == null)
            {
                errors.Add("Tower Combat Rules asset is missing.");
                return errors;
            }

            if (rules.MinimumProcessorCountInValidChain != 0 ||
                rules.MinimumElementCountInValidChain != 0)
            {
                errors.Add("Valid chains cannot require a Processor or Element tower.");
            }

            if (rules.MaximumLinkRangeMeters <= 0f ||
                rules.ProjectileSpeedMetersPerSecond <= 0f ||
                rules.MinimumProcessIntervalSeconds <= 0f)
            {
                errors.Add("Network and projectile distances/speeds must be greater than zero.");
            }

            if (Math.Abs(rules.SimulationTickSeconds - 0.05f) > 0.0001f)
            {
                errors.Add("Tower simulation tick must be exactly 0.05 seconds.");
            }

            if (rules.NormalQueueCapacity <= 0)
            {
                errors.Add("Combat rules require a positive normal queue.");
            }

            if (rules.StartingGold < 0 || rules.NormalWaveReward < 0 ||
                rules.BossWaveReward < rules.NormalWaveReward ||
                rules.SellRefundFraction < 0f || rules.SellRefundFraction > 1f ||
                rules.MaximumTierThreeElementTowers != 2 ||
                rules.MinimumEffectiveDefense < 0f)
            {
                errors.Add("Economy, progression limits, or defense floor are invalid.");
            }

            DefenseResolutionStep[] expectedOrder =
            {
                DefenseResolutionStep.StrongestEarthReduction,
                DefenseResolutionStep.PercentPenetration,
                DefenseResolutionStep.FlatPenetration,
                DefenseResolutionStep.ClampToMinimum,
                DefenseResolutionStep.Mitigation,
                DefenseResolutionStep.DamageTakenModifier
            };
            if (rules.DefenseResolutionOrder == null ||
                rules.DefenseResolutionOrder.Count != expectedOrder.Length)
            {
                errors.Add("Defense Resolution Order is incomplete.");
            }
            else
            {
                for (int index = 0; index < expectedOrder.Length; index++)
                {
                    if (rules.DefenseResolutionOrder[index] != expectedOrder[index])
                    {
                        errors.Add("Defense Resolution Order does not match the authored contract.");
                        break;
                    }
                }
            }

            return errors;
        }

        public static IReadOnlyList<string> CollectErrors(
            TowerCatalog catalog,
            bool requirePlacementDefinitions = true)
        {
            var errors = new List<string>();
            if (catalog == null)
            {
                errors.Add("Tower Catalog asset is missing.");
                return errors;
            }

            errors.AddRange(CollectErrors(catalog.CombatRules));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var families = new HashSet<TowerFamily>();

            if (catalog.Definitions == null)
            {
                errors.Add("Tower Catalog definition list is missing.");
                return errors;
            }

            for (int index = 0; index < catalog.Definitions.Count; index++)
            {
                TowerCombatDefinition definition = catalog.Definitions[index];
                if (definition == null)
                {
                    errors.Add($"Tower Catalog entry {index} is missing.");
                    continue;
                }

                errors.AddRange(CollectErrors(definition, requirePlacementDefinitions));
                TowerCoreProfile core = definition.Core;
                if (core == null)
                {
                    continue;
                }

                if (!ids.Add(core.StableId))
                {
                    errors.Add($"Duplicate Tower Stable Id '{core.StableId}'.");
                }

                if (!families.Add(definition.Family))
                {
                    errors.Add($"Duplicate Tower Family '{definition.Family}'.");
                }
            }

            foreach (TowerFamily family in Enum.GetValues(typeof(TowerFamily)))
            {
                if (!families.Contains(family))
                {
                    errors.Add($"Tower Catalog is missing family '{family}'.");
                }
            }

            return errors;
        }

        private static void ValidateNetwork(
            TowerCombatDefinition definition,
            TowerNetworkProfile network,
            ICollection<string> errors)
        {
            if (network == null)
            {
                errors.Add($"{definition.Family}: Network profile is missing.");
                return;
            }

            int expectedInputs = definition.NetworkRole == TowerNetworkRole.Source ? 0 : 1;
            int expectedOutputs = definition.NetworkRole == TowerNetworkRole.Sink ? 0 : 1;
            if (definition.Family == TowerFamily.SoulNexus)
            {
                expectedInputs = 2;
            }

            if (network.InputPortCount != expectedInputs ||
                network.OutputPortCount != expectedOutputs)
            {
                errors.Add(
                    $"{definition.Family}: expected {expectedInputs} input(s) and " +
                    $"{expectedOutputs} output(s).");
            }

            bool requiresQueue = definition.NetworkRole == TowerNetworkRole.Processor ||
                                 definition.Family == TowerFamily.SoulNexus;
            if (requiresQueue && network.QueueCapacityPerInput <= 0)
            {
                errors.Add($"{definition.Family}: finite input queue capacity is required.");
            }

        }

        private static void ValidateThroughput(
            TowerCombatDefinition definition,
            TowerThroughputProfile throughput,
            ICollection<string> errors)
        {
            if (throughput == null)
            {
                errors.Add($"{definition.Family}: Throughput profile is missing.");
                return;
            }

            if (throughput.BatchSize <= 0 || throughput.ConcurrentLines <= 0)
            {
                errors.Add($"{definition.Family}: Batch Size and Concurrent Lines must be positive.");
            }

            if (throughput.CycleIntervalSeconds <= 0f)
            {
                errors.Add($"{definition.Family}: Cycle Interval must be greater than zero.");
            }
        }

        private static void ValidateEconomy(
            TowerEconomyProfile economy,
            ICollection<string> errors)
        {
            if (economy == null)
            {
                errors.Add("Tower Economy profile is missing.");
                return;
            }

            if (economy.BuildCost < 0 || economy.LinearUpgradeCost < 0 ||
                economy.MaxInstancesPerLevel < 0)
            {
                errors.Add("Tower Economy values cannot be negative.");
            }
        }
    }
}
