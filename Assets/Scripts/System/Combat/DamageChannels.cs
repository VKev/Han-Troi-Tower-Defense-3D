using System;
using TowerDefense3D.Towers;

namespace TowerDefense3D.Enemies
{
    public readonly struct DamageChannels : IEquatable<DamageChannels>
    {
        public DamageChannels(float physical, float magic, float trueDamage = 0f)
        {
            Validate(physical, nameof(physical));
            Validate(magic, nameof(magic));
            Validate(trueDamage, nameof(trueDamage));
            Physical = physical;
            Magic = magic;
            True = trueDamage;
        }

        public float Physical { get; }
        public float Magic { get; }
        public float True { get; }
        public float Total => Physical + Magic + True;

        public static DamageChannels From(float amount, DamageType damageType)
        {
            switch (damageType)
            {
                case DamageType.Physical:
                    return new DamageChannels(amount, 0f);
                case DamageType.Magic:
                    return new DamageChannels(0f, amount);
                case DamageType.True:
                    return new DamageChannels(0f, 0f, amount);
                default:
                    throw new ArgumentOutOfRangeException(nameof(damageType));
            }
        }

        public bool Equals(DamageChannels other)
        {
            return Physical.Equals(other.Physical) && Magic.Equals(other.Magic)
                && True.Equals(other.True);
        }

        public override bool Equals(object obj)
        {
            return obj is DamageChannels other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Physical, Magic, True);
        }

        private static void Validate(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Damage must be finite and non-negative.");
            }
        }
    }

    public readonly struct ResolvedDamage
    {
        public ResolvedDamage(float physical, float magic, float trueDamage)
        {
            Physical = physical;
            Magic = magic;
            True = trueDamage;
        }

        public float Physical { get; }
        public float Magic { get; }
        public float True { get; }
        public float Total => Physical + Magic + True;
    }
}
