using System;
using TowerDefense3D.Core;
using UnityEngine;

namespace TowerDefense3D.Towers
{
    public static class TowerRuntimeSpecFactory
    {
        /// <summary>
        /// Builds the simulation spec for one tower at one upgrade level.
        /// </summary>
        /// <remarks>
        /// A level scales the payload rather than the cycle, so the simulation never has to know
        /// levels exist: it reads the same payload it always did, and an upgraded tower simply
        /// hits harder. Rebuilding the spec is how an upgrade takes effect.
        /// </remarks>
        public static TowerRuntimeSpec Create(
            TowerCombatDefinition definition,
            float tickSeconds,
            int upgradeLevel = 0,
            float defaultProjectileSpeedMetersPerSecond = 10f,
            float defaultRangeMeters = 12f)
        {
            ValidateInput(definition, tickSeconds);

            TowerCoreProfile core = definition.Core;
            TowerNetworkProfile network = core.Network;
            TowerThroughputProfile throughput = core.Throughput;
            SoulNexusDefinition soulNexus = definition as SoulNexusDefinition;
            TowerUpgradeTierProfile tier = definition.UpgradeCosts?.GetTier(upgradeLevel);
            int consumeBatchSize = soulNexus == null ? 0 : throughput.BatchSize;
            SoulConsumeOrder? consumeOrder = soulNexus?.ConsumeOrder;
            float attackIntervalSeconds = tier != null && tier.AttackIntervalSeconds > 0f
                ? tier.AttackIntervalSeconds
                : throughput.CycleIntervalSeconds;
            float projectileSpeedMetersPerSecond = tier != null
                && tier.ProjectileSpeedMetersPerSecond > 0f
                    ? tier.ProjectileSpeedMetersPerSecond
                    : defaultProjectileSpeedMetersPerSecond;
            float rangeMeters = tier != null && tier.RangeMeters > 0f
                ? tier.RangeMeters
                : definition is HeroTowerDefinition hero
                    ? hero.AttackRangeMeters
                    : defaultRangeMeters;
            int cycleTicks = ConvertPositiveSecondsToTicks(attackIntervalSeconds, tickSeconds);
            int outputProjectileCount = GetOutputProjectileCount(definition, throughput);
            int reservationCount = outputProjectileCount;
            int sequenceSpacingTicks = GetSequenceSpacingTicks(
                throughput, outputProjectileCount, tickSeconds);
            ProjectilePayload outputPayload = ApplyTier(
                CreateOutputPayload(definition),
                tier,
                projectileSpeedMetersPerSecond);

            return new TowerRuntimeSpec(
                definition.Family, definition.NetworkRole, core.StableId, network.InputPortCount,
                network.OutputPortCount, network.QueueCapacityPerInput, cycleTicks, outputProjectileCount,
                reservationCount,
                sequenceSpacingTicks,
                outputPayload,
                consumeBatchSize,
                consumeOrder,
                rangeMeters);
        }

        private static void ValidateInput(TowerCombatDefinition definition, float tickSeconds)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (!FiniteNumber.IsFinite(tickSeconds) || tickSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(tickSeconds), "Simulation tick must be finite and positive.");
            }

            TowerCoreProfile core = definition.Core;
            if (core == null)
            {
                throw new InvalidOperationException($"{definition.name} is missing Core data.");
            }

            if (string.IsNullOrWhiteSpace(core.StableId))
            {
                throw new InvalidOperationException($"{definition.name} is missing Stable ID.");
            }

            if (core.Network == null)
            {
                throw new InvalidOperationException($"{definition.name} is missing Network data.");
            }

            if (core.Throughput == null)
            {
                throw new InvalidOperationException($"{definition.name} is missing Throughput data.");
            }
        }

        private static int ConvertPositiveSecondsToTicks(float seconds, float tickSeconds)
        {
            if (!FiniteNumber.IsFinite(seconds) || seconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), "Duration must be finite and positive.");
            }

            decimal exactTickCount = (decimal)seconds / (decimal)tickSeconds;
            return Math.Max(1, decimal.ToInt32(decimal.Ceiling(exactTickCount)));
        }

        // Every emitting tower, Fire included, fires its authored batch size. The Fire Tier One
        // clone burst stays authoring data until tier upgrades are actually simulated.
        private static int GetOutputProjectileCount(TowerCombatDefinition definition, TowerThroughputProfile throughput)
        {
            if (definition is SoulNexusDefinition)
            {
                return 0;
            }

            return throughput.BatchSize;
        }

        private static int GetSequenceSpacingTicks(
            TowerThroughputProfile throughput, int outputProjectileCount, float tickSeconds)
        {
            if (outputProjectileCount <= 1)
            {
                return 0;
            }

            return ConvertNonNegativeSecondsToTicks(throughput.SequenceSpacingSeconds, tickSeconds);
        }

        private static int ConvertNonNegativeSecondsToTicks(float seconds, float tickSeconds)
        {
            if (!FiniteNumber.IsFinite(seconds) || seconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), "Spacing must be finite and non-negative.");
            }

            return seconds == 0f ? 0 : ConvertPositiveSecondsToTicks(seconds, tickSeconds);
        }

        private static ProjectilePayload ApplyTier(
            ProjectilePayload payload,
            TowerUpgradeTierProfile tier,
            float projectileSpeedMetersPerSecond)
        {
            if (tier == null)
            {
                return new ProjectilePayload(
                    payload.Kind,
                    payload.Damage,
                    payload.BurnDamagePerTick,
                    payload.BurnTickIntervalSeconds,
                payload.BurnDurationSeconds,
                payload.PushDistanceMeters,
                projectileSpeedMetersPerSecond,
                payload.SlowStrengthFraction,
                payload.SlowDurationSeconds);
            }

            return new ProjectilePayload(
                payload.Kind,
                payload.Damage * Mathf.Max(1f, tier.DamageMultiplier),
                tier.BurnDamagePerTick,
                tier.BurnTickIntervalSeconds,
                tier.BurnDurationSeconds,
                tier.PushDistanceMeters,
                projectileSpeedMetersPerSecond,
                tier.SlowStrengthFraction,
                tier.SlowDurationSeconds);
        }

        private static ProjectilePayload CreateOutputPayload(TowerCombatDefinition definition)
        {
            switch (definition)
            {
                case GeneratorTowerDefinition generator:
                    return CreateGeneratorPayload(generator);
                case FireTowerDefinition fire:
                    return CreateFirePayload(fire);
                case WaterTowerDefinition water:
                    return CreateWaterPayload(water);
                case WindTowerDefinition wind:
                    return CreateWindPayload(wind);
                case HeroTowerDefinition hero:
                    return CreateHeroPayload(hero);
                case SoulNexusDefinition:
                    return new ProjectilePayload(ProjectilePayloadKind.Basic, 0f);
                default:
                    throw new NotSupportedException(
                        $"Tower type '{definition.GetType().Name}' is not supported by V0 tower simulation.");
            }
        }

        private static ProjectilePayload CreateGeneratorPayload(GeneratorTowerDefinition generator)
        {
            if (generator.Generation == null || generator.Generation.BasicDamage == null)
            {
                throw new InvalidOperationException("Generator basic damage data is missing.");
            }

            DamageProfile damage = generator.Generation.BasicDamage;
            return new ProjectilePayload(ProjectilePayloadKind.Basic, damage.Amount);
        }

        private static ProjectilePayload CreateHeroPayload(HeroTowerDefinition hero)
        {
            if (hero.AttackDamage == null)
            {
                throw new InvalidOperationException("Hero attack damage data is missing.");
            }

            return new ProjectilePayload(ProjectilePayloadKind.Basic, hero.AttackDamage.Amount);
        }

        /// <summary>
        /// Water and Wind used to hard-code zero damage here, so their Game Balance Center
        /// columns would have been decoration. Reading the authored profiles instead means a
        /// designer raising the number sees it land, and leaving it at zero keeps today's
        /// behaviour byte for byte.
        /// </summary>
        private static ProjectilePayload CreateWaterPayload(WaterTowerDefinition water)
        {
            if (water.DirectDamage == null || water.Burn == null)
            {
                throw new InvalidOperationException("Water direct damage or burn data is missing.");
            }

            return new ProjectilePayload(
                ProjectilePayloadKind.Water,
                water.DirectDamage.Amount,
                water.Burn.DamagePerTick,
                water.Burn.TickIntervalSeconds,
                water.Burn.DurationSeconds,
                0f,
                10f,
                water.Slow?.StrengthFraction ?? 0f,
                water.Slow?.DurationSeconds ?? 0f);
        }

        private static ProjectilePayload CreateWindPayload(WindTowerDefinition wind)
        {
            if (wind.DirectDamage == null || wind.Burn == null)
            {
                throw new InvalidOperationException("Wind direct damage or burn data is missing.");
            }

            return new ProjectilePayload(
                ProjectilePayloadKind.Wind,
                wind.DirectDamage.Amount,
                wind.Burn.DamagePerTick,
                wind.Burn.TickIntervalSeconds,
                wind.Burn.DurationSeconds,
                wind.BasePushDistanceMeters);
        }

        private static ProjectilePayload CreateFirePayload(FireTowerDefinition fire)
        {
            if (fire.DirectDamage == null || fire.Burn == null)
            {
                throw new InvalidOperationException("Fire direct damage or burn data is missing.");
            }

            float totalFireDamage = fire.DirectDamage.Amount;
            BurnProfile burn = fire.Burn;
            return new ProjectilePayload(
                ProjectilePayloadKind.Fire,
                totalFireDamage,
                burn.DamagePerTick,
                burn.TickIntervalSeconds,
                burn.DurationSeconds);
        }
    }
}
