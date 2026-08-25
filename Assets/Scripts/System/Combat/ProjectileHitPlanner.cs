using System;
using System.Collections.Generic;
using TowerDefense3D.Towers;
using TowerDefense3D.Waves;
using UnityEngine;

namespace TowerDefense3D.Enemies
{
    internal sealed class ProjectileHitPlanner
    {
        private const double TickBoundaryTolerance = 0.000001d;
        private readonly RoadPath roadPath;
        private readonly float tickSeconds;
        private readonly float projectileSpeedMetersPerSecond;
        private readonly float projectileHitRadius;

        public ProjectileHitPlanner(
            RoadPath roadPath,
            float tickSeconds,
            float projectileSpeedMetersPerSecond,
            float projectileHitRadius)
        {
            this.roadPath = roadPath ?? throw new ArgumentNullException(nameof(roadPath));
            this.tickSeconds = tickSeconds;
            this.projectileSpeedMetersPerSecond = projectileSpeedMetersPerSecond;
            this.projectileHitRadius = projectileHitRadius;
        }

        public Vector3 RoadStart => roadPath.Start;

        public IReadOnlyList<EnemyTrajectoryPlan> CreateWaveEnemyTrajectories(
            IReadOnlyList<WaveSpawnOrder> wavePlan)
        {
            var seeds = new EnemyTrajectorySeed[wavePlan.Count];
            for (int index = 0; index < wavePlan.Count; index++)
            {
                WaveSpawnOrder order = wavePlan[index];
                seeds[index] = new EnemyTrajectorySeed(
                    order.EnemyId,
                    order.Enemy,
                    roadPath.Start,
                    1,
                    Math.Max(1L, SecondsToTick(order.TimeSeconds)));
            }

            return CreateEnemyTrajectories(seeds);
        }

        public IReadOnlyList<EnemyTrajectoryPlan> CreateEnemyTrajectories(
            IReadOnlyList<EnemyTrajectorySeed> seeds)
        {
            if (seeds.Count == 0)
            {
                return Array.Empty<EnemyTrajectoryPlan>();
            }

            var states = new List<EnemyTrajectoryState>(seeds.Count);
            long firstTick = long.MaxValue;
            for (int index = 0; index < seeds.Count; index++)
            {
                EnemyTrajectorySeed seed = seeds[index];
                states.Add(new EnemyTrajectoryState(seed));
                firstTick = Math.Min(firstTick, seed.FirstMovementTick);
            }

            int completedCount = 0;
            long currentTick = firstTick;
            while (completedCount < states.Count)
            {
                for (int index = 0; index < states.Count; index++)
                {
                    EnemyTrajectoryState state = states[index];
                    state.SpeedBonus = IsActive(state, currentTick)
                        ? FindStrongestSpeedBonus(state, states, currentTick)
                        : 0f;
                }

                for (int index = 0; index < states.Count; index++)
                {
                    EnemyTrajectoryState state = states[index];
                    if (!IsActive(state, currentTick))
                    {
                        continue;
                    }

                    Advance(state, currentTick);
                    if (state.IsComplete)
                    {
                        completedCount++;
                    }
                }

                if (currentTick == long.MaxValue)
                {
                    throw new InvalidOperationException("Enemy trajectory tick range has been exhausted.");
                }

                currentTick++;
            }

            var trajectories = new EnemyTrajectoryPlan[states.Count];
            for (int index = 0; index < states.Count; index++)
            {
                EnemyTrajectoryState state = states[index];
                trajectories[index] = new EnemyTrajectoryPlan(
                    state.EnemyId,
                    state.Definition.BaseHitRadius,
                    state.FirstMovementTick,
                    state.Segments);
            }

            return trajectories;
        }

        public ProjectileTrajectoryPlan CreateProjectileTrajectory(
            TowerProjectileSnapshot projectile,
            TowerWorldPosition targetPosition,
            long creationTick)
        {
            Vector3 start = ToVector3(projectile.Position);
            Vector3 end = ToVector3(targetPosition);
            float startTimeSeconds = (creationTick + projectile.LaunchDelayTicks) * tickSeconds;
            float durationSeconds = Vector3.Distance(start, end) / projectileSpeedMetersPerSecond;
            var motion = new TimedTrajectorySegment(
                start,
                end,
                startTimeSeconds,
                startTimeSeconds + durationSeconds);
            long firstMovementTick = creationTick + projectile.LaunchDelayTicks + 1L;
            long lastMovementTick = Math.Max(firstMovementTick, SecondsToTick(motion.EndTimeSeconds));

            return new ProjectileTrajectoryPlan(
                projectile.ProjectileId,
                projectile.Payload,
                motion,
                firstMovementTick,
                lastMovementTick);
        }

        public bool TryCreateScheduledHit(
            ProjectileTrajectoryPlan projectile,
            EnemyTrajectoryPlan enemy,
            out ScheduledProjectileHit scheduledHit)
        {
            IReadOnlyList<TimedTrajectorySegment> enemySegments = enemy.Segments;
            for (int index = 0; index < enemySegments.Count; index++)
            {
                TimedTrajectorySegment enemySegment = enemySegments[index];
                float overlapStart = Mathf.Max(
                    projectile.Motion.StartTimeSeconds,
                    enemySegment.StartTimeSeconds);
                float overlapEnd = Mathf.Min(
                    projectile.Motion.EndTimeSeconds,
                    enemySegment.EndTimeSeconds);
                if (overlapEnd < overlapStart)
                {
                    continue;
                }

                Vector3 projectilePosition = projectile.Motion.Evaluate(overlapStart);
                Vector3 enemyPosition = enemySegment.Evaluate(overlapStart);
                if (!TrajectoryHitCalculator.TryFindFirstIntersectionTimeXZ(
                    projectilePosition,
                    projectile.Motion.Velocity,
                    enemyPosition,
                    enemySegment.Velocity,
                    overlapEnd - overlapStart,
                    projectileHitRadius + enemy.HitRadius,
                    out float elapsedToHit))
                {
                    continue;
                }

                float hitTimeSeconds = overlapStart + elapsedToHit;
                long hitTick = Math.Max(
                    Math.Max(projectile.FirstMovementTick, enemy.FirstMovementTick),
                    SecondsToTick(hitTimeSeconds));
                if (hitTick > projectile.LastMovementTick)
                {
                    break;
                }

                scheduledHit = new ScheduledProjectileHit(
                    projectile.ProjectileId,
                    enemy.EnemyId,
                    hitTick,
                    projectile.Payload,
                    projectile.Motion.Evaluate(hitTimeSeconds));
                return true;
            }

            scheduledHit = default;
            return false;
        }

        public long GetFirstMovementTick(float spawnTimeSeconds)
        {
            return Math.Max(1L, SecondsToTick(spawnTimeSeconds));
        }

        private void Advance(EnemyTrajectoryState state, long currentTick)
        {
            float speed = state.Definition.BaseMoveSpeed * (1f + state.SpeedBonus);
            float remainingDistance = speed * tickSeconds;
            float segmentStartTime = (currentTick - 1L) * tickSeconds;

            while (remainingDistance > 0f && state.TargetPointIndex < roadPath.PointCount)
            {
                Vector3 target = roadPath.GetPoint(state.TargetPointIndex);
                float distanceToTarget = Vector3.Distance(state.Position, target);
                if (distanceToTarget <= float.Epsilon)
                {
                    state.Position = target;
                    state.TargetPointIndex++;
                    continue;
                }

                float travelDistance = Mathf.Min(remainingDistance, distanceToTarget);
                Vector3 nextPosition = Vector3.MoveTowards(state.Position, target, travelDistance);
                float segmentDuration = travelDistance / speed;
                state.Segments.Add(new TimedTrajectorySegment(
                    state.Position,
                    nextPosition,
                    segmentStartTime,
                    segmentStartTime + segmentDuration));
                state.Position = nextPosition;
                segmentStartTime += segmentDuration;
                remainingDistance -= travelDistance;

                if (travelDistance >= distanceToTarget)
                {
                    state.TargetPointIndex++;
                }
            }

            state.IsComplete = state.TargetPointIndex >= roadPath.PointCount;
        }

        private static float FindStrongestSpeedBonus(
            EnemyTrajectoryState target,
            IReadOnlyList<EnemyTrajectoryState> states,
            long currentTick)
        {
            if (target.Definition.Rank == EnemyRank.Boss)
            {
                return 0f;
            }

            float strongestBonus = 0f;
            for (int index = 0; index < states.Count; index++)
            {
                EnemyTrajectoryState source = states[index];
                if (source.EnemyId == target.EnemyId
                    || !IsActive(source, currentTick)
                    || !(source.Definition is SpeedSupportEnemyDefinition support))
                {
                    continue;
                }

                Vector2 offset = new Vector2(
                    source.Position.x - target.Position.x,
                    source.Position.z - target.Position.z);
                if (offset.sqrMagnitude > support.AuraRadiusMeters * support.AuraRadiusMeters)
                {
                    continue;
                }

                float bonus = target.Definition.Rank == EnemyRank.MiniBoss
                    ? support.MiniBossSpeedBonusFraction
                    : support.RegularSpeedBonusFraction;
                strongestBonus = Mathf.Max(strongestBonus, bonus);
            }

            return strongestBonus;
        }

        private static bool IsActive(EnemyTrajectoryState state, long currentTick)
        {
            return !state.IsComplete && state.FirstMovementTick <= currentTick;
        }

        private long SecondsToTick(float timeSeconds)
        {
            return (long)Math.Ceiling(timeSeconds / tickSeconds - TickBoundaryTolerance);
        }

        private static Vector3 ToVector3(TowerWorldPosition position)
        {
            return new Vector3(position.X, position.Y, position.Z);
        }
    }

    internal readonly struct EnemyTrajectorySeed
    {
        public EnemyTrajectorySeed(
            long enemyId,
            EnemyDefinition definition,
            Vector3 position,
            int targetPointIndex,
            long firstMovementTick)
        {
            EnemyId = enemyId;
            Definition = definition;
            Position = position;
            TargetPointIndex = targetPointIndex;
            FirstMovementTick = firstMovementTick;
        }

        public long EnemyId { get; }
        public EnemyDefinition Definition { get; }
        public Vector3 Position { get; }
        public int TargetPointIndex { get; }
        public long FirstMovementTick { get; }
    }

    internal sealed class EnemyTrajectoryState
    {
        public EnemyTrajectoryState(EnemyTrajectorySeed seed)
        {
            EnemyId = seed.EnemyId;
            Definition = seed.Definition;
            Position = seed.Position;
            TargetPointIndex = seed.TargetPointIndex;
            FirstMovementTick = seed.FirstMovementTick;
        }

        public long EnemyId { get; }
        public EnemyDefinition Definition { get; }
        public long FirstMovementTick { get; }
        public List<TimedTrajectorySegment> Segments { get; } = new List<TimedTrajectorySegment>();
        public Vector3 Position { get; set; }
        public int TargetPointIndex { get; set; }
        public float SpeedBonus { get; set; }
        public bool IsComplete { get; set; }
    }

    internal readonly struct TimedTrajectorySegment
    {
        public TimedTrajectorySegment(
            Vector3 start,
            Vector3 end,
            float startTimeSeconds,
            float endTimeSeconds)
        {
            Start = start;
            End = end;
            StartTimeSeconds = startTimeSeconds;
            EndTimeSeconds = endTimeSeconds;
            Velocity = (end - start) / (endTimeSeconds - startTimeSeconds);
        }

        public Vector3 Start { get; }
        public Vector3 End { get; }
        public Vector3 Velocity { get; }
        public float StartTimeSeconds { get; }
        public float EndTimeSeconds { get; }

        public Vector3 Evaluate(float timeSeconds)
        {
            float progress = Mathf.InverseLerp(StartTimeSeconds, EndTimeSeconds, timeSeconds);
            return Vector3.LerpUnclamped(Start, End, progress);
        }
    }

    internal sealed class EnemyTrajectoryPlan
    {
        public EnemyTrajectoryPlan(
            long enemyId,
            float hitRadius,
            long firstMovementTick,
            IReadOnlyList<TimedTrajectorySegment> segments)
        {
            EnemyId = enemyId;
            HitRadius = hitRadius;
            FirstMovementTick = firstMovementTick;
            Segments = segments;
        }

        public long EnemyId { get; }
        public float HitRadius { get; }
        public long FirstMovementTick { get; }
        public IReadOnlyList<TimedTrajectorySegment> Segments { get; }
    }

    internal readonly struct ProjectileTrajectoryPlan
    {
        public ProjectileTrajectoryPlan(
            long projectileId,
            ProjectilePayload payload,
            TimedTrajectorySegment motion,
            long firstMovementTick,
            long lastMovementTick)
        {
            ProjectileId = projectileId;
            Payload = payload;
            Motion = motion;
            FirstMovementTick = firstMovementTick;
            LastMovementTick = lastMovementTick;
        }

        public long ProjectileId { get; }
        public ProjectilePayload Payload { get; }
        public TimedTrajectorySegment Motion { get; }
        public long FirstMovementTick { get; }
        public long LastMovementTick { get; }
    }

    internal readonly struct ScheduledProjectileHit
    {
        public ScheduledProjectileHit(
            long projectileId,
            long enemyId,
            long hitTick,
            ProjectilePayload payload,
            Vector3 position)
        {
            ProjectileId = projectileId;
            EnemyId = enemyId;
            HitTick = hitTick;
            Payload = payload;
            Position = position;
        }

        public long ProjectileId { get; }
        public long EnemyId { get; }
        public long HitTick { get; }
        public ProjectilePayload Payload { get; }
        public Vector3 Position { get; }
    }
}
