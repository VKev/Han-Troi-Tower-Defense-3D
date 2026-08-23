using System;

namespace TowerDefense3D.Towers
{
    public sealed class TowerRuntimeSpec
    {
        public TowerRuntimeSpec(
            TowerFamily family, TowerNetworkRole networkRole, string stableId, int inputPortCount, int outputPortCount,
            int queueCapacityPerInput, int cycleTicks, int outputProjectileCount,
            int requiredDownstreamReservationCount, int sequenceSpacingTicks, ProjectilePayload outputPayload,
            int consumeBatchSize = 0, SoulConsumeOrder? consumeOrder = null)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                throw new ArgumentException("Tower stable ID is required.", nameof(stableId));
            }

            if (inputPortCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(inputPortCount));
            }

            if (outputPortCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(outputPortCount));
            }

            if (queueCapacityPerInput < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(queueCapacityPerInput));
            }

            if (cycleTicks <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cycleTicks), "Cycle ticks must be positive.");
            }

            if (outputProjectileCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(outputProjectileCount));
            }

            if (requiredDownstreamReservationCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(requiredDownstreamReservationCount));
            }

            if (sequenceSpacingTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sequenceSpacingTicks));
            }

            if (consumeBatchSize < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(consumeBatchSize));
            }

            ValidateNetworkRole(networkRole, inputPortCount, outputPortCount, queueCapacityPerInput,
                outputProjectileCount, requiredDownstreamReservationCount, outputPayload);

            ValidateSinkConsumption(networkRole, consumeBatchSize, consumeOrder);

            Family = family;
            NetworkRole = networkRole;
            StableId = stableId;
            InputPortCount = inputPortCount;
            OutputPortCount = outputPortCount;
            QueueCapacityPerInput = queueCapacityPerInput;
            CycleTicks = cycleTicks;
            OutputProjectileCount = outputProjectileCount;
            RequiredDownstreamReservationCount = requiredDownstreamReservationCount;
            SequenceSpacingTicks = sequenceSpacingTicks;
            OutputPayload = outputPayload;
            ConsumeBatchSize = consumeBatchSize;
            ConsumeOrder = consumeOrder;
        }

        public TowerFamily Family { get; }
        public TowerNetworkRole NetworkRole { get; }
        public string StableId { get; }
        public int InputPortCount { get; }
        public int OutputPortCount { get; }
        public int QueueCapacityPerInput { get; }
        public int CycleTicks { get; }
        public int OutputProjectileCount { get; }
        public int RequiredDownstreamReservationCount { get; }
        public int SequenceSpacingTicks { get; }
        public ProjectilePayload OutputPayload { get; }
        public int ConsumeBatchSize { get; }
        public SoulConsumeOrder? ConsumeOrder { get; }

        private static void ValidateNetworkRole(TowerNetworkRole networkRole, int inputPortCount, int outputPortCount,
            int queueCapacityPerInput, int outputProjectileCount, int requiredDownstreamReservationCount,
            ProjectilePayload outputPayload)
        {
            switch (networkRole)
            {
                case TowerNetworkRole.Source:
                    ValidateSource(inputPortCount, outputPortCount, queueCapacityPerInput, outputProjectileCount,
                        requiredDownstreamReservationCount, outputPayload);
                    break;

                case TowerNetworkRole.Processor:
                    ValidateProcessor(inputPortCount, outputPortCount, queueCapacityPerInput, outputProjectileCount,
                        requiredDownstreamReservationCount, outputPayload);
                    break;

                case TowerNetworkRole.Sink:
                    ValidateSink(inputPortCount, outputPortCount, queueCapacityPerInput, outputProjectileCount,
                        requiredDownstreamReservationCount);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(networkRole));
            }
        }

        private static void ValidateSource(int inputPortCount, int outputPortCount, int queueCapacityPerInput,
            int outputProjectileCount, int requiredDownstreamReservationCount, ProjectilePayload outputPayload)
        {
            if (inputPortCount != 0)
            {
                throw new ArgumentException("A source tower cannot have input ports.");
            }

            if (outputPortCount <= 0)
            {
                throw new ArgumentException("A source tower requires an output port.");
            }

            if (queueCapacityPerInput != 0)
            {
                throw new ArgumentException("A source tower does not own an input queue.");
            }

            ValidateProducingTower(outputProjectileCount, requiredDownstreamReservationCount, outputPayload);
        }

        private static void ValidateProcessor(int inputPortCount, int outputPortCount, int queueCapacityPerInput,
            int outputProjectileCount, int requiredDownstreamReservationCount, ProjectilePayload outputPayload)
        {
            if (inputPortCount <= 0)
            {
                throw new ArgumentException("A processor requires at least one input port.");
            }

            if (outputPortCount <= 0)
            {
                throw new ArgumentException("A processor requires an output port.");
            }

            if (queueCapacityPerInput <= 0)
            {
                throw new ArgumentException("A processor requires a positive input queue capacity.");
            }

            ValidateProducingTower(outputProjectileCount, requiredDownstreamReservationCount, outputPayload);
        }

        private static void ValidateSink(int inputPortCount, int outputPortCount, int queueCapacityPerInput,
            int outputProjectileCount, int requiredDownstreamReservationCount)
        {
            if (inputPortCount <= 0)
            {
                throw new ArgumentException("A sink requires at least one input port.");
            }

            if (outputPortCount != 0)
            {
                throw new ArgumentException("A sink cannot have an output port.");
            }

            if (queueCapacityPerInput <= 0)
            {
                throw new ArgumentException("A sink requires a positive input queue capacity.");
            }

            if (outputProjectileCount != 0)
            {
                throw new ArgumentException("A sink cannot emit projectiles.");
            }

            if (requiredDownstreamReservationCount != 0)
            {
                throw new ArgumentException("A sink cannot reserve downstream slots.");
            }
        }

        private static void ValidateProducingTower(int outputProjectileCount, int requiredDownstreamReservationCount,
            ProjectilePayload outputPayload)
        {
            if (outputProjectileCount <= 0)
            {
                throw new ArgumentException("A producing tower must emit at least one projectile.");
            }

            if (requiredDownstreamReservationCount != outputProjectileCount)
            {
                throw new ArgumentException("V0 requires the entire output batch to be reserved atomically.");
            }

            if (outputPayload.Damage <= 0f)
            {
                throw new ArgumentException("A producing tower requires positive output damage.");
            }
        }

        private static void ValidateSinkConsumption(TowerNetworkRole networkRole, int consumeBatchSize, SoulConsumeOrder? consumeOrder)
        {
            if (networkRole == TowerNetworkRole.Sink)
            {
                if (consumeBatchSize <= 0)
                {
                    throw new ArgumentException("A sink requires a positive consume batch size.");
                }

                if (!consumeOrder.HasValue)
                {
                    throw new ArgumentException("A sink requires an input consume order.");
                }

                if (!Enum.IsDefined(typeof(SoulConsumeOrder), consumeOrder.Value))
                {
                    throw new ArgumentOutOfRangeException(nameof(consumeOrder));
                }

                return;
            }

            if (consumeBatchSize != 0 || consumeOrder.HasValue)
            {
                throw new ArgumentException("Only a sink can own input consumption data.");
            }
        }
    }
}
