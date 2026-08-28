using System;
using TowerDefense3D.Core;
using TowerDefense3D.Enemies;

namespace TowerDefense3D.Towers
{
    public enum ProjectilePayloadKind
    {
        Basic,
        Fire,
        Water,
        Wind
    }

    public readonly struct ProjectilePayload
    {
        public ProjectilePayload(
            ProjectilePayloadKind kind,
            float damage,
            float burnDamagePerTick = 0f,
            float burnTickIntervalSeconds = 0f,
            float burnDurationSeconds = 0f,
            float pushDistanceMeters = 0f)
        {
            if (!FiniteNumber.IsFinite(damage) || damage < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(damage), "Projectile damage must be finite and non-negative.");
            }

            Kind = kind;
            Damage = damage;
            BurnDamagePerTick = burnDamagePerTick;
            BurnTickIntervalSeconds = burnTickIntervalSeconds;
            BurnDurationSeconds = burnDurationSeconds;
            PushDistanceMeters = pushDistanceMeters;
        }

        public ProjectilePayloadKind Kind { get; }
        public float Damage { get; }
        public float BurnDamagePerTick { get; }
        public float BurnTickIntervalSeconds { get; }
        public float BurnDurationSeconds { get; }
        public float PushDistanceMeters { get; }
    }

    public readonly struct ProjectileQueueEntry
    {
        public ProjectileQueueEntry(long projectileId, long arrivalTick, ProjectilePayload payload)
        {
            if (projectileId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(projectileId), "Projectile ID must be positive.");
            }

            if (arrivalTick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(arrivalTick), "Arrival tick cannot be negative.");
            }

            ProjectileId = projectileId;
            ArrivalTick = arrivalTick;
            Payload = payload;
        }

        public long ProjectileId { get; }
        public long ArrivalTick { get; }
        public ProjectilePayload Payload { get; }
    }
}
