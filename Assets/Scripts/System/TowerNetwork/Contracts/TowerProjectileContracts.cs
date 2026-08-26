using System;
using TowerDefense3D.Core;
using TowerDefense3D.Enemies;

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
            if (!FiniteNumber.IsFinite(damage) || damage < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(damage), "Projectile damage must be finite and non-negative.");
            }

            Kind = kind;
            DamageChannels = DamageChannels.From(damage, damageType);
            DamageType = damageType;
        }

        public ProjectilePayload(
            ProjectilePayloadKind kind,
            DamageChannels damageChannels)
        {
            Kind = kind;
            DamageChannels = damageChannels;
            DamageType = GetPrimaryDamageType(damageChannels);
        }

        public ProjectilePayloadKind Kind { get; }
        public DamageChannels DamageChannels { get; }
        public float Damage => DamageChannels.Total;
        public DamageType DamageType { get; }

        private static DamageType GetPrimaryDamageType(DamageChannels damage)
        {
            if (damage.Physical > 0f)
            {
                return DamageType.Physical;
            }

            if (damage.Magic > 0f)
            {
                return DamageType.Magic;
            }

            return DamageType.True;
        }
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
