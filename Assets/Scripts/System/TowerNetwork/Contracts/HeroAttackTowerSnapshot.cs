using System;

namespace TowerDefense3D.Towers
{
    public readonly struct HeroAttackTowerSnapshot
    {
        public HeroAttackTowerSnapshot(
            TowerNodeId nodeId,
            TowerWorldPosition position,
            float rangeMeters,
            float damage,
            float aoeRadiusMeters,
            int cycleTicks,
            float prepareDurationSeconds,
            float lungeDurationSeconds,
            float impactHoldDurationSeconds,
            float returnDurationSeconds)
        {
            if (!nodeId.IsValid || rangeMeters <= 0f || damage <= 0f || aoeRadiusMeters < 0f
                || cycleTicks <= 0 || prepareDurationSeconds <= 0f || lungeDurationSeconds < 0f
                || impactHoldDurationSeconds < 0f || returnDurationSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(nodeId));
            }

            NodeId = nodeId;
            Position = position;
            RangeMeters = rangeMeters;
            Damage = damage;
            AoeRadiusMeters = aoeRadiusMeters;
            CycleTicks = cycleTicks;
            PrepareDurationSeconds = prepareDurationSeconds;
            LungeDurationSeconds = lungeDurationSeconds;
            ImpactHoldDurationSeconds = impactHoldDurationSeconds;
            ReturnDurationSeconds = returnDurationSeconds;
        }

        public TowerNodeId NodeId { get; }
        public TowerWorldPosition Position { get; }
        public float RangeMeters { get; }
        public float Damage { get; }
        public float AoeRadiusMeters { get; }
        public int CycleTicks { get; }
        public float PrepareDurationSeconds { get; }
        public float LungeDurationSeconds { get; }
        public float ImpactHoldDurationSeconds { get; }
        public float ReturnDurationSeconds { get; }
    }
}
