using UnityEngine;

namespace TowerDefense3D.Enemies
{
    public readonly struct EnemySnapshot
    {
        public EnemySnapshot(
            long enemyId,
            EnemyDefinition definition,
            Vector3 previousPosition,
            Vector3 position,
            float health,
            bool isHidden,
            bool isSummoned,
            EnemyElementState elementState = default,
            int remainingThermalShieldHits = 0,
            float liftHeightMeters = 0f)
        {
            EnemyId = enemyId;
            Definition = definition;
            PreviousPosition = previousPosition;
            Position = position;
            Health = health;
            IsHidden = isHidden;
            IsSummoned = isSummoned;
            ElementState = elementState;
            RemainingThermalShieldHits = remainingThermalShieldHits;
            LiftHeightMeters = liftHeightMeters;
        }

        public long EnemyId { get; }
        public EnemyDefinition Definition { get; }
        public Vector3 PreviousPosition { get; }
        public Vector3 Position { get; }
        public float Health { get; }
        public bool IsHidden { get; }
        public bool IsSummoned { get; }
        public EnemyElementState ElementState { get; }
        public int RemainingThermalShieldHits { get; }
        public float LiftHeightMeters { get; }
        public bool IsThermalShieldBroken => RemainingThermalShieldHits <= 0;
    }
}
