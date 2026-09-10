using System;
using System.Collections.Generic;
using TowerDefense3D.Economy;
using UnityEngine;

namespace TowerDefense3D.Enemies
{
    public sealed class EnemySystem
    {
        private readonly RoadPathSet roadPaths;
        private readonly LevelGoldSystem goldSystem;
        private readonly LevelBaseHealthSystem healthSystem;
        private readonly List<EnemyInstance> activeEnemies = new List<EnemyInstance>();
        private readonly Dictionary<long, EnemyInstance> enemiesById =
            new Dictionary<long, EnemyInstance>();
        private readonly Dictionary<long, float> speedBonusesByEnemyId =
            new Dictionary<long, float>();
        private readonly List<PendingSummon> pendingSummons = new List<PendingSummon>();
        private long nextEnemyId = 1L;

        public EnemySystem(
            RoadPath roadPath,
            LevelGoldSystem goldSystem,
            LevelBaseHealthSystem healthSystem)
            : this(
                new RoadPathSet(new[] { roadPath }),
                goldSystem,
                healthSystem)
        {
        }

        public EnemySystem(
            RoadPathSet roadPaths,
            LevelGoldSystem goldSystem,
            LevelBaseHealthSystem healthSystem)
        {
            this.roadPaths = roadPaths ?? throw new ArgumentNullException(nameof(roadPaths));
            this.goldSystem = goldSystem ?? throw new ArgumentNullException(nameof(goldSystem));
            this.healthSystem = healthSystem ?? throw new ArgumentNullException(nameof(healthSystem));
        }

        public event Action<EnemySnapshot> EnemySpawned;
        public event Action<EnemySnapshot> EnemyKilled;
        public event Action<EnemySnapshot> EnemyLeaked;

        /// <summary>
        /// Removed without dying and without reaching the end. It pays no reward and costs no
        /// health, which is exactly why it cannot be announced as a kill or as a leak.
        /// </summary>
        public event Action<EnemySnapshot> EnemyDespawned;

        /// <summary>An enemy carried on under a new id: old id first, new id second.</summary>
        public event Action<long, long> EnemyRekeyed;

        /// <summary>
        /// Enemies that hold the wave open. The standing boss is deliberately not one of them: it
        /// is present for the whole level, so counting it would mean wave one never ends.
        /// </summary>
        private readonly List<EnemySnapshot> removalSnapshot = new List<EnemySnapshot>();
        private EnemyInstance standingBoss;
        private IReadOnlyList<float> standingCastTimes = Array.Empty<float>();
        private float standingCastDurationSeconds;
        private int standingNextCastIndex;

        public int LivingCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < activeEnemies.Count; index++)
                {
                    if (activeEnemies[index].IsAlive && !activeEnemies[index].IsStanding)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>Every enemy on the board, standing boss included.</summary>
        public int SpawnedCount => enemiesById.Count;

        /// <summary>Whether an enemy with this id is already on the board.</summary>
        public bool IsSpawned(long enemyId)
        {
            return enemiesById.ContainsKey(enemyId);
        }

        public EnemyInstance Spawn(EnemyDefinition definition)
        {
            long enemyId = ReserveEnemyId();
            RoadPath route = roadPaths.GetForEnemy(enemyId, definition);
            return SpawnAt(enemyId, definition, route.Start, 1, route);
        }

        internal EnemyInstance Spawn(long enemyId, EnemyDefinition definition)
        {
            return Spawn(enemyId, definition, -1);
        }

        internal EnemyInstance Spawn(
            long enemyId,
            EnemyDefinition definition,
            int spawnPointIndex)
        {
            return Spawn(
                enemyId,
                definition,
                spawnPointIndex,
                0f,
                isStanding: false,
                suppressEntranceEffect: false);
        }

        /// <summary>
        /// Spawns an enemy that begins part way along its road, and may be the boss that stands
        /// there.
        /// </summary>
        /// <remarks>
        /// The start point is walked along the road rather than set as a coordinate, exactly as
        /// the planner walks it. Both sides have to arrive at the same place and the same next
        /// waypoint, or the first planned frame would jerk the enemy somewhere else.
        /// </remarks>
        internal EnemyInstance Spawn(
            long enemyId,
            EnemyDefinition definition,
            int spawnPointIndex,
            float startDistanceMeters,
            bool isStanding,
            bool suppressEntranceEffect)
        {
            RoadPath route = roadPaths.GetForEnemy(enemyId, definition, spawnPointIndex);
            Vector3 position = route.Start;
            int targetPointIndex = 1;
            if (startDistanceMeters > 0f)
            {
                route.Move(ref targetPointIndex, ref position, startDistanceMeters);
            }

            // Set before SpawnAt announces the spawn, or the view would be built from a snapshot
            // that still says a fresh arrival and would play the entrance effect anyway.
            EnemyInstance enemy = SpawnAt(
                enemyId,
                definition,
                position,
                targetPointIndex,
                route,
                isStanding: isStanding,
                suppressEntranceEffect: suppressEntranceEffect);
            return enemy;
        }

        /// <summary>
        /// Puts the level's standing boss on the road, or leaves the one already there, and gives
        /// it this wave's cast times.
        /// </summary>
        /// <remarks>
        /// It is spawned outside the combat plan on purpose. The plan finishes when its enemy list
        /// empties, and a boss that cannot be hurt and never moves would keep that list occupied
        /// until the tick horizon ran out and the wave was refused. Since it takes no part in
        /// combat until the wave it fights on, it does not belong in the deterministic path at
        /// all - only the enemies it summons do, and those are planned as ordinary spawns.
        ///
        /// The same instance is kept from wave to wave rather than respawned. Waves are separated
        /// by a preparation phase of whatever length the player likes, and a boss that vanished
        /// between waves and reappeared would read as a bug.
        /// </remarks>
        internal void EnsureStandingBoss(
            EnemyDefinition definition,
            int spawnPointIndex,
            float standDistanceMeters,
            float facingYawDegrees,
            IReadOnlyList<float> castTimesSeconds,
            float castDurationSeconds)
        {
            if (definition == null)
            {
                return;
            }

            if (standingBoss == null || !standingBoss.IsAlive)
            {
                standingBoss = Spawn(
                    ReserveEnemyId(),
                    definition,
                    spawnPointIndex,
                    standDistanceMeters,
                    isStanding: true,
                    suppressEntranceEffect: false);
            }

            // Re-read every wave, so turning the marker takes effect on the next wave rather than
            // only on a fresh level.
            standingBoss.FacingYawDegrees = facingYawDegrees;

            standingCastTimes = castTimesSeconds ?? Array.Empty<float>();
            standingCastDurationSeconds = castDurationSeconds;
            standingNextCastIndex = 0;
        }

        /// <summary>Whether a standing boss is on the board to be taken over.</summary>
        internal bool HasStandingBoss => standingBoss != null && standingBoss.IsAlive;

        /// <summary>
        /// Hands the standing boss to the combat plan under the id the plan issued for it.
        /// </summary>
        /// <remarks>
        /// The instance carries on: the boss the player has been looking at all level is the boss
        /// that walks away, with no swap to notice. Only its id changes, and it has to - the plan
        /// numbers what it owns, and its summon ids continue from the highest id it issued. A boss
        /// keeping an older, lower id would leave the plan's numbering and the live counter out of
        /// step, and every enemy the boss summoned would be planned under an id nothing on the
        /// board answers to.
        ///
        /// The view is moved across too, or it would go on answering to the id it was spawned
        /// under and stop receiving the frames that now drive its enemy.
        /// </remarks>
        internal bool AdoptStandingBossAs(long plannedEnemyId)
        {
            if (!HasStandingBoss)
            {
                return false;
            }

            EnemyInstance adopted = standingBoss;
            long previousId = adopted.Id;

            enemiesById.Remove(previousId);
            adopted.Id = plannedEnemyId;
            enemiesById[plannedEnemyId] = adopted;
            adopted.IsStanding = false;
            adopted.SkillCastRemainingSeconds = 0f;

            standingBoss = null;
            standingCastTimes = Array.Empty<float>();
            standingNextCastIndex = 0;

            EnemyRekeyed?.Invoke(previousId, plannedEnemyId);
            return true;
        }

        /// <summary>
        /// Takes the standing boss off the board, for the wave it joins the fight on.
        /// </summary>
        internal void RemoveStandingBoss()
        {
            if (standingBoss == null)
            {
                return;
            }

            enemiesById.Remove(standingBoss.Id);
            activeEnemies.Remove(standingBoss);
            PublishEnemyDespawned(CreateSnapshot(standingBoss));
            standingBoss = null;
            standingCastTimes = Array.Empty<float>();
            standingNextCastIndex = 0;
        }

        /// <summary>
        /// Runs the standing boss's cast clock, against the wave's own elapsed time.
        /// </summary>
        /// <remarks>
        /// Driven by the wave rather than by this system's <see cref="Step"/>, because that method
        /// is never called during play - every other enemy is moved by the frames the combat plan
        /// recorded, so the live step survives only for tests. A cast clock put there would never
        /// tick and the boss would stand silent all level.
        ///
        /// The wave's elapsed time is also the clock the cast times are authored against, so
        /// reading it directly removes a second counter that could drift from it.
        ///
        /// Live rather than planned is allowed here precisely because nothing depends on it: the
        /// boss deals no damage and takes none, so the cast is animation and nothing else. What it
        /// appears to produce was placed in the wave plan at the matching times.
        /// </remarks>
        internal void StepStandingBossCasts(float waveElapsedSeconds, float stepSeconds)
        {
            if (standingBoss == null || !standingBoss.IsAlive)
            {
                return;
            }

            if (standingBoss.SkillCastRemainingSeconds > 0f)
            {
                standingBoss.SkillCastRemainingSeconds = Mathf.Max(
                    0f,
                    standingBoss.SkillCastRemainingSeconds - stepSeconds);
                return;
            }

            while (standingNextCastIndex < standingCastTimes.Count
                && standingCastTimes[standingNextCastIndex] <= waveElapsedSeconds)
            {
                standingNextCastIndex++;
                standingBoss.SkillCastRemainingSeconds = standingCastDurationSeconds;
                standingBoss.SkillCastVersion++;
            }
        }

        /// <summary>
        /// How far along a road the point nearest <paramref name="worldPosition"/> lies.
        /// </summary>
        /// <remarks>
        /// Lives here because this is where the roads are. It projects rather than requiring the
        /// point to be on the road, so a marker dropped roughly in place still resolves to
        /// somewhere the boss can stand.
        /// </remarks>
        internal float MeasureRoadDistance(int spawnPointIndex, Vector3 worldPosition)
        {
            // Enemy ids start at one, and route selection rejects anything lower. There is no real
            // enemy to ask about here - ids are handed out after the plan is built - so the first
            // id is used to pick a route deterministically. Where a spawn point owns a single
            // route, which is the ordinary case, every id gives the same answer anyway.
            int routeIndex = roadPaths.GetRouteIndex(1L, spawnPointIndex);

            // The centre lane, because a boss always walks it.
            RoadPath road = roadPaths.GetLane(routeIndex, RoadPathSet.CenterLaneIndex);
            float travelled = 0f;
            float best = 0f;
            float bestSquared = float.MaxValue;

            for (int index = 1; index < road.PointCount; index++)
            {
                Vector3 from = road.GetPoint(index - 1);
                Vector3 to = road.GetPoint(index);
                Vector3 segment = to - from;
                float lengthSquared = segment.sqrMagnitude;
                if (lengthSquared <= 0.000001f)
                {
                    continue;
                }

                float length = Mathf.Sqrt(lengthSquared);
                float along = Mathf.Clamp01(Vector3.Dot(worldPosition - from, segment) / lengthSquared);
                Vector3 candidate = from + (segment * along);
                float squared = (candidate - worldPosition).sqrMagnitude;
                if (squared < bestSquared)
                {
                    bestSquared = squared;
                    best = travelled + (length * along);
                }

                travelled += length;
            }

            return best;
        }

        internal long ReserveEnemyId()
        {
            if (nextEnemyId == long.MaxValue)
            {
                throw new InvalidOperationException("Enemy identifier range has been exhausted.");
            }

            return nextEnemyId++;
        }

        internal void SpawnPlannedSummon(PlannedEnemySpawn spawn)
        {
            long enemyId = ReserveEnemyId();
            if (enemyId != spawn.EnemyId)
            {
                throw new InvalidOperationException(
                    $"Planned summon expected Enemy {spawn.EnemyId}, but reserved {enemyId}.");
            }

            SpawnAt(
                enemyId,
                spawn.Definition,
                spawn.Position,
                spawn.TargetPointIndex,
                roadPaths.GetLane(
                    spawn.RouteIndex,
                    roadPaths.GetLaneIndex(enemyId, spawn.Definition)),
                isSummoned: true);
        }

        internal void ApplyPlannedFrame(PlannedEnemyFrame frame)
        {
            // A wave reset or level teardown can remove an enemy after its deterministic timeline
            // was built but before the last queued frame is replayed. The frame is stale in that
            // case; treating it as a no-op keeps cleanup idempotent instead of crashing the player
            // loop with a dictionary lookup for an entity that has already gone away.
            if (!enemiesById.TryGetValue(frame.EnemyId, out EnemyInstance enemy))
            {
                return;
            }
            enemy.PreviousPosition = frame.PreviousPosition;
            enemy.Position = frame.Position;
            enemy.Health = frame.Health;
            enemy.RevealRemainingSeconds = frame.RevealRemainingSeconds;
            enemy.SkillCastVersion = frame.SkillCastVersion;
            enemy.IsSpeedBuffed = frame.IsSpeedBuffed;
            enemy.TargetPointIndex = frame.TargetPointIndex;
            enemy.ElementState = new EnemyElementState(
                frame.ElementPhase,
                frame.Element,
                frame.ElementRemainingSeconds);
            enemy.RemainingThermalShieldHits = frame.RemainingThermalShieldHits;
            enemy.LiftHeightMeters = frame.LiftHeightMeters;

            if (frame.Removal == PlannedEnemyRemoval.None)
            {
                return;
            }

            enemiesById.Remove(enemy.Id);
            activeEnemies.Remove(enemy);
            EnemySnapshot snapshot = CreateSnapshot(enemy);
            if (frame.Removal == PlannedEnemyRemoval.Killed)
            {
                PublishEnemyKilled(snapshot);
            }
            else
            {
                PublishEnemyLeaked(snapshot);
            }
        }

        public void Step(float stepSeconds)
        {
            pendingSummons.Clear();
            for (int index = activeEnemies.Count - 1; index >= 0; index--)
            {
                EnemyInstance enemy = activeEnemies[index];
                if (!enemy.IsAlive)
                {
                    activeEnemies.RemoveAt(index);
                    continue;
                }

                enemy.SkillCastCompletedThisStep = false;
                UpdateReveal(enemy, stepSeconds);
                UpdateSpeedSupport(enemy, stepSeconds);
                QueueBossSummons(enemy, stepSeconds);
            }

            speedBonusesByEnemyId.Clear();
            for (int index = 0; index < activeEnemies.Count; index++)
            {
                EnemyInstance enemy = activeEnemies[index];
                if (!enemy.IsAlive)
                {
                    continue;
                }

                float speedBonus = FindStrongestSpeedBonus(enemy);
                speedBonusesByEnemyId.Add(enemy.Id, speedBonus);
                enemy.IsSpeedBuffed = speedBonus > 0f;
            }

            for (int index = activeEnemies.Count - 1; index >= 0; index--)
            {
                EnemyInstance enemy = activeEnemies[index];
                enemy.PreviousPosition = enemy.Position;

                // The standing boss never advances.
                if (enemy.IsStanding)
                {
                    continue;
                }

                float movementSeconds = stepSeconds;
                if (enemy.SpawnDelayRemainingSeconds > 0f)
                {
                    float heldSeconds = Mathf.Min(
                        enemy.SpawnDelayRemainingSeconds,
                        movementSeconds);
                    enemy.SpawnDelayRemainingSeconds -= heldSeconds;
                    movementSeconds -= heldSeconds;
                    if (movementSeconds <= 0f)
                    {
                        continue;
                    }
                }

                if (enemy.SkillCastRemainingSeconds > 0f || enemy.SkillCastCompletedThisStep)
                {
                    continue;
                }

                float speedMultiplier = 1f + speedBonusesByEnemyId[enemy.Id];
                float distance = enemy.Definition.BaseMoveSpeed * speedMultiplier * movementSeconds;
                Vector3 position = enemy.Position;
                int targetPointIndex = enemy.TargetPointIndex;
                bool reachedEnd = enemy.Route.Move(ref targetPointIndex, ref position, distance);
                enemy.Position = position;
                enemy.TargetPointIndex = targetPointIndex;

                if (reachedEnd)
                {
                    enemiesById.Remove(enemy.Id);
                    activeEnemies.RemoveAt(index);
                    PublishEnemyLeaked(CreateSnapshot(enemy));
                    continue;
                }
            }

            SpawnPendingSummons();
        }

        public bool TryGetEnemy(long enemyId, out EnemyInstance enemy)
        {
            return enemiesById.TryGetValue(enemyId, out enemy);
        }

        public bool ApplyDamage(long enemyId, float damage)
        {
            if (!enemiesById.TryGetValue(enemyId, out EnemyInstance enemy))
            {
                return false;
            }

            enemy.Health = Mathf.Max(0f, enemy.Health - damage);
            if (enemy.IsAlive)
            {
                return false;
            }

            enemiesById.Remove(enemyId);
            PublishEnemyKilled(CreateSnapshot(enemy));
            return true;
        }

        public void RevealFromDirectHit(long enemyId)
        {
            EnemyInstance enemy = enemiesById[enemyId];
            if (enemy.Definition is StealthEnemyDefinition stealth)
            {
                enemy.RevealRemainingSeconds = stealth.RevealDurationSeconds;
            }
        }

        public void CopySnapshotsTo(List<EnemySnapshot> destination)
        {
            destination.Clear();
            for (int index = 0; index < activeEnemies.Count; index++)
            {
                EnemyInstance enemy = activeEnemies[index];
                if (enemy.IsAlive)
                {
                    destination.Add(CreateSnapshot(enemy));
                }
            }
        }

        /// <summary>
        /// Clears the wave off the board, leaving the level's own fixtures standing.
        /// </summary>
        /// <remarks>
        /// Every enemy that leaves is announced. Clearing the lists silently left their views on
        /// screen, still registered under ids the counter was about to hand out again - which is
        /// how a fresh enemy collided with a view nobody had let go, and how one that did get
        /// reused inherited the pose and animation of whoever held it last.
        ///
        /// The standing boss stays. It belongs to the level rather than to any wave - which is
        /// why wave completion does not count it either - so wiping a wave must leave it exactly
        /// where the player has been watching it since the first one.
        ///
        /// The id counter is not rewound. It used to restart at one, which was harmless while
        /// nothing outlived a wave; with a boss standing on id one, the next wave's first enemy
        /// was handed the same id. Ids are longs, so letting the counter only ever climb costs
        /// nothing.
        /// </remarks>
        public void Reset()
        {
            // Copied first: the removal is published while iterating, and a listener is free to
            // call back into this system.
            removalSnapshot.Clear();
            for (int index = 0; index < activeEnemies.Count; index++)
            {
                EnemyInstance enemy = activeEnemies[index];
                if (!enemy.IsStanding)
                {
                    removalSnapshot.Add(CreateSnapshot(enemy));
                }
            }

            for (int index = 0; index < removalSnapshot.Count; index++)
            {
                long enemyId = removalSnapshot[index].EnemyId;
                if (enemiesById.TryGetValue(enemyId, out EnemyInstance enemy))
                {
                    enemiesById.Remove(enemyId);
                    activeEnemies.Remove(enemy);
                }
            }

            speedBonusesByEnemyId.Clear();
            pendingSummons.Clear();

            for (int index = 0; index < removalSnapshot.Count; index++)
            {
                PublishEnemyDespawned(removalSnapshot[index]);
            }

            removalSnapshot.Clear();
        }

        private EnemyInstance SpawnAt(
            long enemyId,
            EnemyDefinition definition,
            Vector3 position,
            int targetPointIndex,
            RoadPath route,
            bool isSummoned = false,
            bool isStanding = false,
            bool suppressEntranceEffect = false)
        {
            var enemy = new EnemyInstance(enemyId, definition, position)
            {
                IsSummoned = isSummoned,
                IsStanding = isStanding,
                SuppressEntranceEffect = suppressEntranceEffect,
                TargetPointIndex = targetPointIndex,
                Route = route
            };
            activeEnemies.Add(enemy);
            enemiesById.Add(enemy.Id, enemy);
            EnemySpawned?.Invoke(CreateSnapshot(enemy));
            return enemy;
        }

        private float FindStrongestSpeedBonus(EnemyInstance target)
        {
            if (target.Definition.Rank == EnemyRank.Boss)
            {
                return 0f;
            }

            float strongestBonus = 0f;
            int stackCount = 0;
            for (int index = 0; index < activeEnemies.Count; index++)
            {
                EnemyInstance source = activeEnemies[index];
                if (ReferenceEquals(source, target)
                    || !source.IsAlive
                    || !source.IsSpeedAuraActive
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
                stackCount++;
            }

            return SpeedSupportEnemyDefinition.CalculateStackedBonus(strongestBonus, stackCount);
        }

        private void QueueBossSummons(EnemyInstance boss, float stepSeconds)
        {
            if (!(boss.Definition is SummonerBossEnemyDefinition definition))
            {
                return;
            }

            // The standing boss summons on the wave's timetable, and those summons are in the
            // combat plan already. Letting the health-driven phases run for it as well would
            // summon everything twice - and the second set would be spawned live, outside the
            // plan, which is precisely the kind of enemy the precomputed timeline cannot account
            // for. It also drives its own cast clock, which this would fight over.
            if (boss.IsStanding)
            {
                return;
            }

            if (boss.SummonCastRemainingSeconds > 0f)
            {
                boss.SummonCastRemainingSeconds = Mathf.Max(
                    0f,
                    boss.SummonCastRemainingSeconds - stepSeconds);
                if (boss.SummonCastRemainingSeconds <= 0f)
                {
                    boss.SkillCastCompletedThisStep = true;
                }
                boss.SkillCastRemainingSeconds = boss.SummonCastRemainingSeconds;
                ReleaseDueSummons(definition, boss);
                return;
            }

            int phaseIndex = FindSummonPhase(definition, boss.HealthFraction);
            if (phaseIndex != boss.SummonPhaseIndex)
            {
                boss.SummonPhaseIndex = phaseIndex;

                // Kept identical to the planner's rule. This copy is only reached by tests today,
                // but a summoning rule that differs between the two is the one thing that would
                // make the plan and the wave disagree.
                boss.SummonElapsedSeconds =
                    definition.SummonPhases[phaseIndex].SummonIntervalSeconds;
            }

            SummonerBossEnemyDefinition.SummonPhase phase = definition.SummonPhases[phaseIndex];
            boss.SummonElapsedSeconds += stepSeconds;
            while (boss.SummonElapsedSeconds >= phase.SummonIntervalSeconds)
            {
                boss.SummonElapsedSeconds -= phase.SummonIntervalSeconds;
                boss.SummonCastRemainingSeconds = definition.SummonSkillDurationSeconds;
                boss.SkillCastRemainingSeconds = definition.SummonSkillDurationSeconds;
                boss.SkillCastVersion++;
                boss.SummonsSpawnedThisCast = 0;
                BossSummonSchedule.Build(
                    definition,
                    phase,
                    boss.Id,
                    boss.SkillCastVersion,
                    boss.SummonSchedule);
                break;
            }
        }

        /// <summary>
        /// Queues every summon whose turn has arrived. Positions are read now rather than when the
        /// cast began, so each one steps out beside wherever the boss has walked to.
        /// </summary>
        private void ReleaseDueSummons(
            SummonerBossEnemyDefinition definition,
            EnemyInstance boss)
        {
            float elapsed = definition.SummonSkillDurationSeconds
                - boss.SummonCastRemainingSeconds;
            while (boss.SummonsSpawnedThisCast < boss.SummonSchedule.Count
                && boss.SummonSchedule[boss.SummonsSpawnedThisCast].DueSeconds <= elapsed)
            {
                ScheduledSummon scheduled = boss.SummonSchedule[boss.SummonsSpawnedThisCast];
                boss.SummonsSpawnedThisCast++;
                BossSummonSchedule.GetSpawnPlacement(
                    boss.Route,
                    boss.TargetPointIndex,
                    boss.Position,
                    scheduled.ForwardOffsetMeters,
                    out Vector3 spawnPosition,
                    out int spawnTargetPointIndex);
                pendingSummons.Add(new PendingSummon(
                    scheduled.Definition,
                    spawnPosition,
                    spawnTargetPointIndex,
                    boss.Route));
            }
        }

        private void SpawnPendingSummons()
        {
            for (int index = 0; index < pendingSummons.Count; index++)
            {
                PendingSummon summon = pendingSummons[index];
                SpawnAt(
                    ReserveEnemyId(),
                    summon.Definition,
                    summon.Position,
                    summon.TargetPointIndex,
                    summon.Route,
                    isSummoned: true);
            }
        }

        private static int FindSummonPhase(
            SummonerBossEnemyDefinition definition,
            float healthFraction)
        {
            int selectedPhase = 0;
            for (int index = 1; index < definition.SummonPhases.Count; index++)
            {
                if (healthFraction > definition.SummonPhases[index].StartHealthFraction)
                {
                    break;
                }

                selectedPhase = index;
            }

            return selectedPhase;
        }

        private static void UpdateReveal(EnemyInstance enemy, float stepSeconds)
        {
            enemy.RevealRemainingSeconds = Mathf.Max(
                0f,
                enemy.RevealRemainingSeconds - stepSeconds);
        }

        private static void UpdateSpeedSupport(EnemyInstance enemy, float stepSeconds)
        {
            if (!(enemy.Definition is SpeedSupportEnemyDefinition support))
            {
                return;
            }

            if (enemy.SupportActivationRemainingSeconds > 0f)
            {
                enemy.SupportActivationRemainingSeconds = Mathf.Max(
                    0f,
                    enemy.SupportActivationRemainingSeconds - stepSeconds);
                if (enemy.SupportActivationRemainingSeconds > 0f)
                {
                    return;
                }

            }

            if (!enemy.IsSpeedAuraActive)
            {
                enemy.SkillCastRemainingSeconds = support.SkillDurationSeconds;
                enemy.SkillCastVersion++;
                enemy.IsSpeedAuraActive = true;
                return;
            }

            if (enemy.SkillCastRemainingSeconds <= 0f)
            {
                return;
            }

            enemy.SkillCastRemainingSeconds = Mathf.Max(
                0f,
                enemy.SkillCastRemainingSeconds - stepSeconds);
            if (enemy.SkillCastRemainingSeconds <= 0f)
            {
                enemy.SkillCastCompletedThisStep = true;
            }
        }

        private static EnemySnapshot CreateSnapshot(EnemyInstance enemy)
        {
            return new EnemySnapshot(
                enemy.Id,
                enemy.Definition,
                enemy.PreviousPosition,
                enemy.Position,
                enemy.Health,
                enemy.IsHidden,
                enemy.IsSummoned,
                enemy.ElementState,
                enemy.RemainingThermalShieldHits,
                enemy.LiftHeightMeters,
                enemy.SkillCastVersion,
                enemy.IsSpeedBuffed,
                enemy.IsStanding,
                enemy.FacingYawDegrees,
                enemy.SuppressEntranceEffect);
        }

        private void PublishEnemyDespawned(EnemySnapshot snapshot)
        {
            EnemyDespawned?.Invoke(snapshot);
        }

        private void PublishEnemyKilled(EnemySnapshot snapshot)
        {
            goldSystem.Add(snapshot.Definition.GoldOnDeath);
            EnemyKilled?.Invoke(snapshot);
        }

        private void PublishEnemyLeaked(EnemySnapshot snapshot)
        {
            healthSystem.TakeDamage(snapshot.Definition.LeakDamage);
            EnemyLeaked?.Invoke(snapshot);
        }

        private readonly struct PendingSummon
        {
            public PendingSummon(
                EnemyDefinition definition,
                Vector3 position,
                int targetPointIndex,
                RoadPath route)
            {
                Definition = definition;
                Position = position;
                TargetPointIndex = targetPointIndex;
                Route = route;
            }

            public EnemyDefinition Definition { get; }
            public Vector3 Position { get; }
            public int TargetPointIndex { get; }
            public RoadPath Route { get; }
        }
    }
}
