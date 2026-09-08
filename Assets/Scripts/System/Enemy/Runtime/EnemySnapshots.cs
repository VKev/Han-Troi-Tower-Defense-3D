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
            float liftHeightMeters = 0f,
            int skillCastVersion = 0,
            bool isSpeedBuffed = false,
            bool isStanding = false,
            float facingYawDegrees = 0f,
            bool suppressEntranceEffect = false)
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
            SkillCastVersion = skillCastVersion;
            IsSpeedBuffed = isSpeedBuffed;
            IsStanding = isStanding;
            FacingYawDegrees = facingYawDegrees;
            SuppressEntranceEffect = suppressEntranceEffect;
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
        public int SkillCastVersion { get; }
        public bool IsSpeedBuffed { get; }

        /// <summary>
        /// The boss that stands on the road. It never advances, so it must not be shown walking.
        /// </summary>
        public bool IsStanding { get; }

        /// <summary>Which way a standing enemy looks, in degrees of yaw.</summary>
        public float FacingYawDegrees { get; }

        /// <summary>Shows up with no entrance effect, because it is continuing not arriving.</summary>
        public bool SuppressEntranceEffect { get; }
        public bool IsThermalShieldBroken => RemainingThermalShieldHits <= 0;
    }
}
