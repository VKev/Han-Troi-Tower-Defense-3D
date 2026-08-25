using System;

namespace TowerDefense3D.Towers
{
    public readonly struct TowerLinkSnapshot
    {
        public TowerLinkSnapshot(TowerNodeId source, TowerNodeId target, int targetInputPort)
        {
            if (!source.IsValid)
            {
                throw new ArgumentException("Link source must be valid.", nameof(source));
            }

            if (!target.IsValid)
            {
                throw new ArgumentException("Link target must be valid.", nameof(target));
            }

            if (targetInputPort < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(targetInputPort));
            }

            Source = source;
            Target = target;
            TargetInputPort = targetInputPort;
        }

        public TowerNodeId Source { get; }
        public TowerNodeId Target { get; }
        public int TargetInputPort { get; }
    }

    public readonly struct TowerProjectileSnapshot
    {
        public TowerProjectileSnapshot(
            long projectileId, TowerNodeId source, TowerNodeId target, TowerWorldPosition position,
            ProjectilePayload payload, int launchDelayTicks)
        {
            if (projectileId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(projectileId), "Projectile ID must be positive.");
            }

            if (!source.IsValid)
            {
                throw new ArgumentException("Projectile source must be valid.", nameof(source));
            }

            if (!target.IsValid)
            {
                throw new ArgumentException("Projectile target must be valid.", nameof(target));
            }

            if (launchDelayTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(launchDelayTicks));
            }

            ProjectileId = projectileId;
            Source = source;
            Target = target;
            Position = position;
            Payload = payload;
            LaunchDelayTicks = launchDelayTicks;
        }

        public long ProjectileId { get; }
        public TowerNodeId Source { get; }
        public TowerNodeId Target { get; }
        public TowerWorldPosition Position { get; }
        public ProjectilePayload Payload { get; }
        public int LaunchDelayTicks { get; }
    }

    public readonly struct TowerProjectileMotionSnapshot
    {
        public TowerProjectileMotionSnapshot(
            long projectileId,
            TowerWorldPosition previousPosition,
            TowerWorldPosition position,
            ProjectilePayload payload)
        {
            if (projectileId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(projectileId));
            }

            ProjectileId = projectileId;
            PreviousPosition = previousPosition;
            Position = position;
            Payload = payload;
        }

        public long ProjectileId { get; }
        public TowerWorldPosition PreviousPosition { get; }
        public TowerWorldPosition Position { get; }
        public ProjectilePayload Payload { get; }
    }

    public readonly struct TowerInputPortSnapshot
    {
        public TowerInputPortSnapshot(
            TowerNodeId nodeId, int inputPort, int queuedProjectileCount, int reservedProjectileCount, int capacity)
        {
            if (!nodeId.IsValid)
            {
                throw new ArgumentException("Node ID must be valid.", nameof(nodeId));
            }

            if (inputPort < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(inputPort));
            }

            if (queuedProjectileCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(queuedProjectileCount));
            }

            if (reservedProjectileCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(reservedProjectileCount));
            }

            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            if (queuedProjectileCount > capacity || reservedProjectileCount > capacity - queuedProjectileCount)
            {
                throw new ArgumentException("Queued and reserved slots exceed capacity.");
            }

            NodeId = nodeId;
            InputPort = inputPort;
            QueuedProjectileCount = queuedProjectileCount;
            ReservedProjectileCount = reservedProjectileCount;
            Capacity = capacity;
        }

        public TowerNodeId NodeId { get; }
        public int InputPort { get; }
        public int QueuedProjectileCount { get; }
        public int ReservedProjectileCount { get; }
        public int Capacity { get; }
        public int OccupiedSlotCount => QueuedProjectileCount + ReservedProjectileCount;
        public int AvailableSlotCount => Capacity - OccupiedSlotCount;
    }

    public readonly struct TowerNodeSimulationSnapshot
    {
        public TowerNodeSimulationSnapshot(
            TowerNodeId nodeId, int cycleProgressTicks, int cycleTicks, bool belongsToValidChain)
        {
            if (!nodeId.IsValid)
            {
                throw new ArgumentException("Node ID must be valid.", nameof(nodeId));
            }

            if (cycleTicks <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cycleTicks), "Cycle ticks must be positive.");
            }

            if (cycleProgressTicks < 0 || cycleProgressTicks > cycleTicks)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cycleProgressTicks), "Cycle progress must stay between zero and the cycle duration.");
            }

            NodeId = nodeId;
            CycleProgressTicks = cycleProgressTicks;
            CycleTicks = cycleTicks;
            BelongsToValidChain = belongsToValidChain;
        }

        public TowerNodeId NodeId { get; }
        public int CycleProgressTicks { get; }
        public int CycleTicks { get; }
        public bool BelongsToValidChain { get; }
        public bool IsReady => CycleProgressTicks >= CycleTicks;
        public int RemainingCycleTicks => CycleTicks - CycleProgressTicks;
    }

    public readonly struct TowerQueueSummary
    {
        public TowerQueueSummary(int queuedProjectileCount, int reservedProjectileCount, int capacity)
        {
            if (queuedProjectileCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(queuedProjectileCount));
            }

            if (reservedProjectileCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(reservedProjectileCount));
            }

            if (capacity < 0 || queuedProjectileCount + reservedProjectileCount > capacity)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            QueuedProjectileCount = queuedProjectileCount;
            ReservedProjectileCount = reservedProjectileCount;
            Capacity = capacity;
        }

        public int QueuedProjectileCount { get; }
        public int ReservedProjectileCount { get; }
        public int Capacity { get; }
        public int AvailableSlotCount => Capacity - QueuedProjectileCount - ReservedProjectileCount;
    }
}
