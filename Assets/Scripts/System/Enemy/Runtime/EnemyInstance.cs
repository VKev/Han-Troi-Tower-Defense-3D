using System;
using System.Collections.Generic;
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
            SpawnDelayRemainingSeconds = EnemySpawnPresentationTiming.SpawnMovementDelaySeconds;
            TargetPointIndex = 1;
            RemainingThermalShieldHits = definition.ThermalShockHitsToBreakShield;
            SupportActivationRemainingSeconds = definition is SpeedSupportEnemyDefinition support
                ? support.ActivationDelaySeconds
                : 0f;
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
        public bool IsSpeedAuraActive { get; internal set; }
        public bool IsSpeedBuffed { get; internal set; }
        public int SkillCastVersion { get; internal set; }
        internal RoadPath Route { get; set; }
        internal int TargetPointIndex { get; set; }
        internal int SummonPhaseIndex { get; set; } = -1;
        internal float SummonElapsedSeconds { get; set; }
        internal float SummonCastRemainingSeconds { get; set; }
        internal float SupportActivationRemainingSeconds { get; set; }
        internal float SpawnDelayRemainingSeconds { get; set; }
        internal float SkillCastRemainingSeconds { get; set; }
        internal List<ScheduledSummon> SummonSchedule { get; } = new List<ScheduledSummon>();
        internal int SummonsSpawnedThisCast { get; set; }
        internal bool SkillCastCompletedThisStep { get; set; }
    }
}
