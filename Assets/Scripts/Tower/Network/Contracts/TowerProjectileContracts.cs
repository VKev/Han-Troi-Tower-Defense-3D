using System;

namespace TowerDefense3D.Towers
{
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
        public ProjectilePayload(ProjectilePayloadKind kind, float damage, DamageType damageType)
        {
            if (float.IsNaN(damage) || float.IsInfinity(damage) || damage < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(damage), "Projectile damage must be finite and non-negative.");
            }

            Kind = kind;
            Damage = damage;
            DamageType = damageType;
        }

        public ProjectilePayloadKind Kind { get; }
        public float Damage { get; }
        public DamageType DamageType { get; }
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
