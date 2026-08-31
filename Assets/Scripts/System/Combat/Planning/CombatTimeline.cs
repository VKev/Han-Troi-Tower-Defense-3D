using System;
using System.Collections.Generic;
using TowerDefense3D.Towers;
using UnityEngine;

namespace TowerDefense3D.Enemies
{
    internal enum PlannedEnemyRemoval
    {
        None,
        Killed,
        Leaked
    }

    internal readonly struct PlannedEnemySpawn
    {
        public PlannedEnemySpawn(
            long enemyId,
            EnemyDefinition definition,
            Vector3 position,
            int targetPointIndex,
            int routeIndex)
        {
            EnemyId = enemyId;
            Definition = definition;
            Position = position;
            TargetPointIndex = targetPointIndex;
            RouteIndex = routeIndex;
        }

        public long EnemyId { get; }
        public EnemyDefinition Definition { get; }
        public Vector3 Position { get; }
        public int TargetPointIndex { get; }
        public int RouteIndex { get; }
    }

    internal readonly struct PlannedEnemyFrame
    {
        public PlannedEnemyFrame(
            long enemyId,
            Vector3 previousPosition,
            Vector3 position,
            float health,
            float revealRemainingSeconds,
            int targetPointIndex,
            EnemyElementPhase elementPhase,
            ElementType element,
            float elementRemainingSeconds,
            int remainingThermalShieldHits,
            float liftHeightMeters,
            int skillCastVersion,
            bool isSpeedBuffed,
            PlannedEnemyRemoval removal)
        {
            EnemyId = enemyId;
            PreviousPosition = previousPosition;
            Position = position;
            Health = health;
            RevealRemainingSeconds = revealRemainingSeconds;
            TargetPointIndex = targetPointIndex;
            ElementPhase = elementPhase;
            Element = element;
            ElementRemainingSeconds = elementRemainingSeconds;
            RemainingThermalShieldHits = remainingThermalShieldHits;
            LiftHeightMeters = liftHeightMeters;
            SkillCastVersion = skillCastVersion;
            IsSpeedBuffed = isSpeedBuffed;
            Removal = removal;
        }

        public long EnemyId { get; }
        public Vector3 PreviousPosition { get; }
        public Vector3 Position { get; }
        public float Health { get; }
        public float RevealRemainingSeconds { get; }
        public int TargetPointIndex { get; }
        public EnemyElementPhase ElementPhase { get; }
        public ElementType Element { get; }
        public float ElementRemainingSeconds { get; }
        public int RemainingThermalShieldHits { get; }
        public float LiftHeightMeters { get; }
        public int SkillCastVersion { get; }
        public bool IsSpeedBuffed { get; }
        public PlannedEnemyRemoval Removal { get; }
    }

    internal readonly struct PlannedReactionEvent
    {
        public PlannedReactionEvent(
            long enemyId,
            ElementReactionId reactionId,
            ElementPair pair,
            Vector3 position,
            float durationSeconds)
        {
            EnemyId = enemyId;
            ReactionId = reactionId;
            Pair = pair;
            Position = position;
            DurationSeconds = durationSeconds;
        }

        public long EnemyId { get; }
        public ElementReactionId ReactionId { get; }
        public ElementPair Pair { get; }
        public Vector3 Position { get; }
        public float DurationSeconds { get; }
    }

    public readonly struct HeroAttackEvent
    {
        public HeroAttackEvent(
            TowerNodeId towerNodeId,
            Vector3 impactPosition,
            float prepareDurationSeconds,
            float lungeDurationSeconds,
            float impactHoldDurationSeconds,
            float returnDurationSeconds)
        {
            TowerNodeId = towerNodeId;
            ImpactPosition = impactPosition;
            PrepareDurationSeconds = prepareDurationSeconds;
            LungeDurationSeconds = lungeDurationSeconds;
            ImpactHoldDurationSeconds = impactHoldDurationSeconds;
            ReturnDurationSeconds = returnDurationSeconds;
        }

        public TowerNodeId TowerNodeId { get; }
        public Vector3 ImpactPosition { get; }
        public float PrepareDurationSeconds { get; }
        public float LungeDurationSeconds { get; }
        public float ImpactHoldDurationSeconds { get; }
        public float ReturnDurationSeconds { get; }
    }

    internal sealed class CombatTimeline
    {
        private readonly Dictionary<long, List<PlannedEnemySpawn>> spawnsByTick =
            new Dictionary<long, List<PlannedEnemySpawn>>();
        private readonly Dictionary<long, List<PlannedEnemyFrame>> framesByTick =
            new Dictionary<long, List<PlannedEnemyFrame>>();
        private readonly Dictionary<long, List<ProjectileImpactEvent>> impactsByTick =
            new Dictionary<long, List<ProjectileImpactEvent>>();
        private readonly Dictionary<long, List<PlannedReactionEvent>> reactionsByTick =
            new Dictionary<long, List<PlannedReactionEvent>>();
        private readonly Dictionary<long, List<HeroAttackEvent>> heroAttacksByTick =
            new Dictionary<long, List<HeroAttackEvent>>();

        public IReadOnlyList<PlannedEnemySpawn> GetSpawns(long tick)
        {
            return Get(spawnsByTick, tick);
        }

        public IReadOnlyList<PlannedEnemyFrame> GetFrames(long tick)
        {
            return Get(framesByTick, tick);
        }

        public IReadOnlyList<ProjectileImpactEvent> GetImpacts(long tick)
        {
            return Get(impactsByTick, tick);
        }

        public IReadOnlyList<PlannedReactionEvent> GetReactions(long tick)
        {
            return Get(reactionsByTick, tick);
        }

        public IReadOnlyList<HeroAttackEvent> GetHeroAttacks(long tick)
        {
            return Get(heroAttacksByTick, tick);
        }

        public void Add(long tick, PlannedEnemySpawn spawn)
        {
            GetOrCreate(spawnsByTick, tick).Add(spawn);
        }

        public void Add(long tick, PlannedEnemyFrame frame)
        {
            GetOrCreate(framesByTick, tick).Add(frame);
        }

        public void Add(long tick, ProjectileImpactEvent impact)
        {
            GetOrCreate(impactsByTick, tick).Add(impact);
        }

        public void Add(long tick, PlannedReactionEvent reaction)
        {
            GetOrCreate(reactionsByTick, tick).Add(reaction);
        }

        public void Add(long tick, HeroAttackEvent attack)
        {
            GetOrCreate(heroAttacksByTick, tick).Add(attack);
        }

        private static IReadOnlyList<T> Get<T>(Dictionary<long, List<T>> source, long tick)
        {
            return source.TryGetValue(tick, out List<T> values)
                ? values
                : Array.Empty<T>();
        }

        private static List<T> GetOrCreate<T>(Dictionary<long, List<T>> source, long tick)
        {
            if (!source.TryGetValue(tick, out List<T> values))
            {
                values = new List<T>();
                source.Add(tick, values);
            }

            return values;
        }
    }
}
