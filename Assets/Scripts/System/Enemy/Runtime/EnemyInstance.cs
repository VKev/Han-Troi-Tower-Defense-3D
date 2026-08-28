using System;
using UnityEngine;

namespace TowerDefense3D.Enemies
{
    public sealed class EnemyInstance
    {
        internal EnemyInstance(long id, EnemyDefinition definition, Vector3 position)
        {
            if (id <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(id));
            }

            Id = id;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Health = definition.BaseMaxHealth;
            Position = position;
            PreviousPosition = position;
            TargetPointIndex = 1;
            RemainingThermalShieldHits = definition.ThermalShockHitsToBreakShield;
        }

        public long Id { get; }
        public EnemyDefinition Definition { get; }
        public float Health { get; internal set; }
        public float HealthFraction => Health / Definition.BaseMaxHealth;
        public Vector3 Position { get; internal set; }
        public Vector3 PreviousPosition { get; internal set; }
        public bool IsAlive => Health > 0f;
        public bool IsSummoned { get; internal set; }
        public bool IsHidden => Definition is StealthEnemyDefinition
            && RevealRemainingSeconds <= 0f;
        public float RevealRemainingSeconds { get; internal set; }
        public EnemyElementState ElementState { get; internal set; }
        public int RemainingThermalShieldHits { get; internal set; }
        public float LiftHeightMeters { get; internal set; }
        public bool IsThermalShieldBroken => RemainingThermalShieldHits <= 0;
        internal int TargetPointIndex { get; set; }
        internal int SummonPhaseIndex { get; set; } = -1;
        internal float SummonElapsedSeconds { get; set; }
    }
}
