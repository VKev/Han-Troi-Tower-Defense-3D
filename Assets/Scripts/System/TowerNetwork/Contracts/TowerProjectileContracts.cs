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
            float pushDistanceMeters = 0f,
            float projectileSpeedMetersPerSecond = 10f,
            float slowStrengthFraction = 0f,
            float slowDurationSeconds = 0f)
        {
            if (!FiniteNumber.IsFinite(damage) || damage < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(damage), "Projectile damage must be finite and non-negative.");
            }

            if (!FiniteNumber.IsFinite(projectileSpeedMetersPerSecond)
                || projectileSpeedMetersPerSecond <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(projectileSpeedMetersPerSecond));
            }

            if (!FiniteNumber.IsFinite(slowStrengthFraction)
                || slowStrengthFraction < 0f || slowStrengthFraction > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(slowStrengthFraction));
            }

            if (!FiniteNumber.IsFinite(slowDurationSeconds) || slowDurationSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(slowDurationSeconds));
            }

            Kind = kind;
            Damage = damage;
            BurnDamagePerTick = burnDamagePerTick;
            BurnTickIntervalSeconds = burnTickIntervalSeconds;
            BurnDurationSeconds = burnDurationSeconds;
            PushDistanceMeters = pushDistanceMeters;
            ProjectileSpeedMetersPerSecond = projectileSpeedMetersPerSecond;
            SlowStrengthFraction = slowStrengthFraction;
            SlowDurationSeconds = slowDurationSeconds;
        }

        public ProjectilePayloadKind Kind { get; }
        public float Damage { get; }
        public float BurnDamagePerTick { get; }
        public float BurnTickIntervalSeconds { get; }
        public float BurnDurationSeconds { get; }
        public float PushDistanceMeters { get; }
        public float ProjectileSpeedMetersPerSecond { get; }
        public float SlowStrengthFraction { get; }
        public float SlowDurationSeconds { get; }
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
