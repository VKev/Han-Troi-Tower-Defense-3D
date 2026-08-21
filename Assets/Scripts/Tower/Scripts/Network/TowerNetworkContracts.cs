using System;

namespace TowerDefense3D.Towers
{


    public readonly struct TowerNodeId : IEquatable<TowerNodeId>
    {
        public TowerNodeId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool IsValid => Value > 0;

        public bool Equals(TowerNodeId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is TowerNodeId other &&
                   Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }

    public readonly struct TowerWorldPosition
    {
        public TowerWorldPosition(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }

        public static float Distance(TowerWorldPosition first, TowerWorldPosition second)
        {
            float deltaX = second.X - first.X;
            float deltaY = second.Y - first.Y;
            float deltaZ = second.Z - first.Z;

            float squaredDistance = (deltaX * deltaX) + (deltaY * deltaY) + (deltaZ * deltaZ);

            return (float)Math.Sqrt(squaredDistance);
        }

        public static TowerWorldPosition MoveTowards(TowerWorldPosition current, TowerWorldPosition target, float maximumDistanceDelta)
        {
            if (maximumDistanceDelta < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumDistanceDelta),
                    "Movement distance cannot be negative.");
            }

            float deltaX = target.X - current.X;
            float deltaY = target.Y - current.Y;
            float deltaZ = target.Z - current.Z;

            float distance = (float)Math.Sqrt(
                (deltaX * deltaX) +
                (deltaY * deltaY) +
                (deltaZ * deltaZ));

            if (distance <= maximumDistanceDelta ||
                distance <= float.Epsilon)
            {
                return target;
            }

            float scale = maximumDistanceDelta / distance;

            return new TowerWorldPosition(
                current.X + (deltaX * scale),
                current.Y + (deltaY * scale),
                current.Z + (deltaZ * scale));
        }
    }


    public enum ProjectilePayloadKind
    {
        Physical,
        Fire,
        Water,
        Wind,
        Earth
    }

    public readonly struct ProjectilePayload
    {
        public ProjectilePayload(
            ProjectilePayloadKind kind, float damage, DamageType damageType)
        {
            if (float.IsNaN(damage) || float.IsInfinity(damage) || damage < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(damage),
                    "Projectile damage must be finite and non-negative.");
            }

            Kind = kind;
            Damage = damage;
            DamageType = damageType;
        }

        public ProjectilePayloadKind Kind { get; }
        public float Damage { get; }
        public DamageType DamageType { get; }
    }

    public readonly struct TowerLinkSnapshot
    {
        public TowerNodeId Source { get; }
        public TowerNodeId Target { get; }
        public int TargetInputPort { get; }
        public TowerLinkSnapshot(
            TowerNodeId source,
            TowerNodeId target,
            int targetInputPort)
        {
            if (!source.IsValid)
            {
                throw new ArgumentException(
                    "Link source must be valid.",
                    nameof(source));
            }

            if (!target.IsValid)
            {
                throw new ArgumentException(
                    "Link target must be valid.",
                    nameof(target));
            }

            if (targetInputPort < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(targetInputPort));
            }

            Source = source;
            Target = target;
            TargetInputPort = targetInputPort;
        }

    }

    public readonly struct TowerProjectileSnapshot
    {
        public long ProjectileId { get; }
        public TowerNodeId Source { get; }
        public TowerNodeId Target { get; }
        public TowerWorldPosition Position { get; }
        public ProjectilePayload Payload { get; }
        public int LaunchDelayTicks { get; }
        public TowerProjectileSnapshot(
            long projectileId,
            TowerNodeId source,
            TowerNodeId target,
            TowerWorldPosition position,
            ProjectilePayload payload,
            int launchDelayTicks)
        {
            if (projectileId <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(projectileId),
                    "Projectile ID must be positive.");
            }

            if (!source.IsValid)
            {
                throw new ArgumentException(
                    "Projectile source must be valid.",
                    nameof(source));
            }

            if (!target.IsValid)
            {
                throw new ArgumentException(
                    "Projectile target must be valid.",
                    nameof(target));
            }

            if (launchDelayTicks < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(launchDelayTicks));
            }

            ProjectileId = projectileId;
            Source = source;
            Target = target;
            Position = position;
            Payload = payload;
            LaunchDelayTicks = launchDelayTicks;
        }


    }

    public sealed class TowerRuntimeSpec
    {
        public TowerRuntimeSpec(
            TowerFamily family,
            TowerNetworkRole networkRole,
            string stableId,
            int inputPortCount,
            int outputPortCount,
            int queueCapacityPerInput,
            int cycleTicks,
            int outputProjectileCount,
            int requiredDownstreamReservationCount,
            int sequenceSpacingTicks,
            ProjectilePayload outputPayload)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                throw new ArgumentException(
                    "Tower stable ID is required.",
                    nameof(stableId));
            }

            if (inputPortCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(inputPortCount));
            }

            if (outputPortCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(outputPortCount));
            }

            if (queueCapacityPerInput < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(queueCapacityPerInput));
            }

            if (cycleTicks <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cycleTicks),
                    "Cycle ticks must be positive.");
            }

            if (outputProjectileCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(outputProjectileCount));
            }

            if (requiredDownstreamReservationCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(requiredDownstreamReservationCount));
            }

            if (sequenceSpacingTicks < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sequenceSpacingTicks));
            }

            ValidateNetworkRole(
                networkRole,
                inputPortCount,
                outputPortCount,
                queueCapacityPerInput,
                outputProjectileCount,
                requiredDownstreamReservationCount,
                outputPayload);

            Family = family;
            NetworkRole = networkRole;
            StableId = stableId;
            InputPortCount = inputPortCount;
            OutputPortCount = outputPortCount;
            QueueCapacityPerInput = queueCapacityPerInput;
            CycleTicks = cycleTicks;
            OutputProjectileCount = outputProjectileCount;
            RequiredDownstreamReservationCount =
                requiredDownstreamReservationCount;
            SequenceSpacingTicks = sequenceSpacingTicks;
            OutputPayload = outputPayload;
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

        private static void ValidateNetworkRole(
            TowerNetworkRole networkRole,
            int inputPortCount,
            int outputPortCount,
            int queueCapacityPerInput,
            int outputProjectileCount,
            int requiredDownstreamReservationCount,
            ProjectilePayload outputPayload)
        {
            switch (networkRole)
            {
                case TowerNetworkRole.Source:
                    ValidateSource(
                        inputPortCount,
                        outputPortCount,
                        queueCapacityPerInput,
                        outputProjectileCount,
                        requiredDownstreamReservationCount,
                        outputPayload);
                    break;

                case TowerNetworkRole.Processor:
                    ValidateProcessor(
                        inputPortCount,
                        outputPortCount,
                        queueCapacityPerInput,
                        outputProjectileCount,
                        requiredDownstreamReservationCount,
                        outputPayload);
                    break;

                case TowerNetworkRole.Sink:
                    ValidateSink(
                        inputPortCount,
                        outputPortCount,
                        queueCapacityPerInput,
                        outputProjectileCount,
                        requiredDownstreamReservationCount);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(networkRole));
            }
        }

        private static void ValidateSource(
            int inputPortCount,
            int outputPortCount,
            int queueCapacityPerInput,
            int outputProjectileCount,
            int requiredDownstreamReservationCount,
            ProjectilePayload outputPayload)
        {
            if (inputPortCount != 0)
            {
                throw new ArgumentException(
                    "A source tower cannot have input ports.");
            }

            if (outputPortCount <= 0)
            {
                throw new ArgumentException(
                    "A source tower requires an output port.");
            }

            if (queueCapacityPerInput != 0)
            {
                throw new ArgumentException(
                    "A source tower does not own an input queue.");
            }

            ValidateProducingTower(
                outputProjectileCount,
                requiredDownstreamReservationCount,
                outputPayload);
        }

        private static void ValidateProcessor(
            int inputPortCount,
            int outputPortCount,
            int queueCapacityPerInput,
            int outputProjectileCount,
            int requiredDownstreamReservationCount,
            ProjectilePayload outputPayload)
        {
            if (inputPortCount <= 0)
            {
                throw new ArgumentException(
                    "A processor requires at least one input port.");
            }

            if (outputPortCount <= 0)
            {
                throw new ArgumentException(
                    "A processor requires an output port.");
            }

            if (queueCapacityPerInput <= 0)
            {
                throw new ArgumentException(
                    "A processor requires a positive input queue capacity.");
            }

            ValidateProducingTower(
                outputProjectileCount,
                requiredDownstreamReservationCount,
                outputPayload);
        }

        private static void ValidateSink(
            int inputPortCount,
            int outputPortCount,
            int queueCapacityPerInput,
            int outputProjectileCount,
            int requiredDownstreamReservationCount)
        {
            if (inputPortCount <= 0)
            {
                throw new ArgumentException(
                    "A sink requires at least one input port.");
            }

            if (outputPortCount != 0)
            {
                throw new ArgumentException(
                    "A sink cannot have an output port.");
            }

            if (queueCapacityPerInput <= 0)
            {
                throw new ArgumentException(
                    "A sink requires a positive input queue capacity.");
            }

            if (outputProjectileCount != 0)
            {
                throw new ArgumentException(
                    "A sink cannot emit projectiles.");
            }

            if (requiredDownstreamReservationCount != 0)
            {
                throw new ArgumentException(
                    "A sink cannot reserve downstream slots.");
            }
        }

        private static void ValidateProducingTower(
            int outputProjectileCount,
            int requiredDownstreamReservationCount,
            ProjectilePayload outputPayload)
        {
            if (outputProjectileCount <= 0)
            {
                throw new ArgumentException(
                    "A producing tower must emit at least one projectile.");
            }

            if (requiredDownstreamReservationCount !=
                outputProjectileCount)
            {
                throw new ArgumentException(
                    "V0 requires the entire output batch to be reserved atomically.");
            }

            if (outputPayload.Damage <= 0f)
            {
                throw new ArgumentException(
                    "A producing tower requires positive output damage.");
            }
        }
    }
}