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

        /// <summary>
        /// Settable only so the standing boss can be re-keyed when the combat plan takes it over.
        /// </summary>
        /// <remarks>
        /// The plan hands out its own ids, and its summon ids continue from the highest one it
        /// issued. Letting the boss keep an older, lower id would put the plan's ids and the live
        /// counter out of step, and the summons it schedules would be looked up under ids nothing
        /// on the board answers to.
        /// </remarks>
        public long Id { get; internal set; }
        public EnemyDefinition Definition { get; }
        public float Health { get; internal set; }
        public float HealthFraction => Health / Definition.BaseMaxHealth;
        public Vector3 Position { get; internal set; }
        public Vector3 PreviousPosition { get; internal set; }
        public bool IsAlive => Health > 0f;
        public bool IsSummoned { get; internal set; }

        /// <summary>
        /// The boss standing on the road for this wave. It does not move, cannot be hurt, and does
        /// not hold the wave open.
        /// </summary>
        public bool IsStanding { get; internal set; }

        /// <summary>Which way a standing enemy looks. Meaningless for one that walks.</summary>
        public float FacingYawDegrees { get; internal set; }

        /// <summary>Arrived without an entrance effect, because it was already there.</summary>
        public bool SuppressEntranceEffect { get; internal set; }
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
