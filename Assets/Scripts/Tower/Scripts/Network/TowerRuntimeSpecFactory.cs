using System;
namespace TowerDefense3D.Towers
{
    public static class TowerRuntimeSpecFactory
    {


        public static TowerRuntimeSpec Create(TowerCombatDefinition definition, float tickSeconds)
        {
            ValidateInput(definition, tickSeconds);

            TowerCoreProfile core = definition.Core;
            TowerNetworkProfile network = core.Network;
            TowerThroughputProfile throughput = core.Throughput;

            int cycleTicks = ConvertPositiveSecondsToTicks(throughput.CycleIntervalSeconds, tickSeconds);
            int outputProjectileCount = GetOutputProjectileCount(definition, throughput);
            int reservationCount = GetRequiredReservationCount(definition, outputProjectileCount);
            int sequenceSpacingTicks = GetSequenceSpacingTicks(definition, throughput, outputProjectileCount, tickSeconds);

            ProjectilePayload outputPayload = CreateOutputPayload(definition);

            return new TowerRuntimeSpec(
                definition.Family,
                definition.NetworkRole,
                core.StableId,
                network.InputPortCount,
                network.OutputPortCount,
                network.QueueCapacityPerInput,
                cycleTicks,
                outputProjectileCount,
                reservationCount,
                sequenceSpacingTicks,
                outputPayload);
        }


        private static void ValidateInput(TowerCombatDefinition definition, float tickSeconds)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(
                    nameof(definition));
            }

            if (float.IsNaN(tickSeconds) ||
                float.IsInfinity(tickSeconds) ||
                tickSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(tickSeconds),
                    "Simulation tick must be finite and positive.");
            }

            TowerCoreProfile core = definition.Core;
            if (core == null)
            {
                throw new InvalidOperationException(
                    $"{definition.name} is missing Core data.");
            }

            if (string.IsNullOrWhiteSpace(core.StableId))
            {
                throw new InvalidOperationException(
                    $"{definition.name} is missing Stable ID.");
            }

            if (core.Network == null)
            {
                throw new InvalidOperationException(
                    $"{definition.name} is missing Network data.");
            }

            if (core.Throughput == null)
            {
                throw new InvalidOperationException(
                    $"{definition.name} is missing Throughput data.");
            }
        }


        private static int ConvertPositiveSecondsToTicks(float seconds, float tickSeconds)
        {
            if (float.IsNaN(seconds) ||
                float.IsInfinity(seconds) ||
                seconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(seconds),
                    "Duration must be finite and positive.");
            }

            decimal duration = (decimal)seconds;
            decimal tick = (decimal)tickSeconds;
            decimal exactTickCount = duration / tick;

            return Math.Max(
                1,
                decimal.ToInt32(
                    decimal.Ceiling(exactTickCount)));
        }

        private static int GetOutputProjectileCount(TowerCombatDefinition definition, TowerThroughputProfile throughput)
        {
            if (definition is SoulNexusDefinition)
            {
                return 0;
            }

            if (definition is FireTowerDefinition fire)
            {
                if (fire.TierOne == null)
                {
                    throw new InvalidOperationException(
                        "Fire Tier One data is missing.");
                }

                return fire.TierOne.OutputProjectileCount;
            }

            return throughput.BatchSize;
        }

        private static int GetRequiredReservationCount(
            TowerCombatDefinition definition,
            int outputProjectileCount)
        {
            if (definition is SoulNexusDefinition)
            {
                return 0;
            }

            if (definition is FireTowerDefinition fire)
            {
                return fire.TierOne.RequiredDownstreamReservationCount;
            }

            return outputProjectileCount;
        }

        private static int GetSequenceSpacingTicks(
            TowerCombatDefinition definition,
            TowerThroughputProfile throughput,
            int outputProjectileCount,
            float tickSeconds)
        {
            if (outputProjectileCount <= 1)
            {
                return 0;
            }

            float spacingSeconds;

            if (definition is FireTowerDefinition fire)
            {
                spacingSeconds =
                    fire.TierOne.SequenceSpacingSeconds;
            }
            else
            {
                spacingSeconds =
                    throughput.SequenceSpacingSeconds;
            }

            return ConvertNonNegativeSecondsToTicks(
                spacingSeconds,
                tickSeconds);
        }

        private static int ConvertNonNegativeSecondsToTicks(
            float seconds,
            float tickSeconds)
        {
            if (float.IsNaN(seconds) ||
                float.IsInfinity(seconds) ||
                seconds < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(seconds),
                    "Spacing must be finite and non-negative.");
            }

            if (seconds == 0f)
            {
                return 0;
            }

            return ConvertPositiveSecondsToTicks(
                seconds,
                tickSeconds);
        }


        private static ProjectilePayload CreateOutputPayload(
            TowerCombatDefinition definition)
        {
            switch (definition)
            {
                case GeneratorTowerDefinition generator:
                    return CreateGeneratorPayload(generator);

                case FireTowerDefinition fire:
                    return CreateFirePayload(fire);

                case WaterTowerDefinition water:
                    return CreateElementPayload(ProjectilePayloadKind.Water, water.DirectDamage, "Water");

                case WindTowerDefinition wind:
                    return CreateElementPayload(ProjectilePayloadKind.Wind, wind.DirectDamage, "Wind");

                case EarthTowerDefinition earth:
                    return CreateElementPayload(ProjectilePayloadKind.Earth, earth.DirectDamage, "Earth");

                case SoulNexusDefinition:
                    return new ProjectilePayload(ProjectilePayloadKind.Physical, 0f, DamageType.Physical);

                default:
                    throw new NotSupportedException(
                        $"Tower type '{definition.GetType().Name}' " +
                        "is not supported by V0 tower simulation.");
            }
        }

        private static ProjectilePayload CreateGeneratorPayload(
            GeneratorTowerDefinition generator)
        {
            if (generator.Generation == null ||
                generator.Generation.PhysicalDamage == null)
            {
                throw new InvalidOperationException(
                    "Generator physical damage data is missing.");
            }

            DamageProfile damage =
                generator.Generation.PhysicalDamage;

            return new ProjectilePayload(
                ProjectilePayloadKind.Physical,
                damage.Amount,
                damage.DamageType);
        }

        private static ProjectilePayload CreateFirePayload(
            FireTowerDefinition fire)
        {
            if (fire.DirectDamage == null ||
                fire.TierOne == null)
            {
                throw new InvalidOperationException(
                    "Fire direct damage or Tier One data is missing.");
            }

            float totalFireDamage =
                fire.DirectDamage.Amount *
                fire.TierOne.DirectFireDamageMultiplier;

            return new ProjectilePayload(
                ProjectilePayloadKind.Fire,
                totalFireDamage,
                fire.DirectDamage.DamageType);
        }

        private static ProjectilePayload CreateElementPayload(
            ProjectilePayloadKind kind,
            DamageProfile damage,
            string displayName)
        {
            if (damage == null)
            {
                throw new InvalidOperationException(
                    $"{displayName} direct damage data is missing.");
            }

            return new ProjectilePayload(
                kind,
                damage.Amount,
                damage.DamageType);
        }
    }
}