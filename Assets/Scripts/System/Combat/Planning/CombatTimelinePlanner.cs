using System;
using System.Collections.Generic;
using TowerDefense3D.Towers;
using TowerDefense3D.Waves;
using UnityEngine;

namespace TowerDefense3D.Enemies
{
    public sealed class CombatTimelinePlanner
    {
        private const float ProjectileHitRadius = 0.2f;
        private const long ImpactMergeWindowTicks = 4L;
        private const float ImpactMergeDistanceMeters = 1.25f;

        private readonly TowerNetworkManager towerNetworkManager;
        private readonly RoadPathSet roadPaths;
        private readonly ElementReactionCatalog reactionCatalog;
        private readonly float tickSeconds;
        private readonly float projectileSpeed;
        private readonly float maximumPushSpeedFraction;

        public CombatTimelinePlanner(
            TowerNetworkManager towerNetworkManager,
            RoadPath roadPath,
            ElementReactionCatalog reactionCatalog)
            : this(
                towerNetworkManager,
                new RoadPathSet(new[] { roadPath }),
                reactionCatalog)
        {
        }

        public CombatTimelinePlanner(
            TowerNetworkManager towerNetworkManager,
            RoadPathSet roadPaths,
            ElementReactionCatalog reactionCatalog)
        {
            this.towerNetworkManager = towerNetworkManager;
            this.roadPaths = roadPaths;
            this.reactionCatalog = reactionCatalog;
            tickSeconds = towerNetworkManager.TickSeconds;
            projectileSpeed = towerNetworkManager.ProjectileSpeedMetersPerSecond;
            maximumPushSpeedFraction = Mathf.Clamp01(towerNetworkManager.MaximumPushSpeedFraction);
        }

        internal CombatTimeline Create(IReadOnlyList<WaveSpawnOrder> wavePlan)
        {
            long horizonTick = CalculatePlanningHorizon(wavePlan);
            IReadOnlyList<TowerProjectileSpawnOrder> projectilePlan =
                towerNetworkManager.EnsureProjectileSpawnPlanThrough(horizonTick);
            var timeline = new CombatTimeline();
            var enemies = new List<ShadowEnemy>();
            var projectiles = new List<ShadowProjectile>();
            var pendingSummons = new List<ShadowSummon>();
            var hitCandidates = new List<ShadowHit>();
            var lastImpacts = new Dictionary<long, ProjectileImpactHistory>();
            int nextWaveSpawnIndex = 0;
            int nextProjectileSpawnIndex = 0;
            long nextEnemyId = FindNextEnemyId(wavePlan);

            for (long tick = 1L; tick <= horizonTick; tick++)
            {
                SpawnWaveEnemies(wavePlan, enemies, tick, ref nextWaveSpawnIndex);
                SpawnProjectiles(
                    projectilePlan,
                    projectiles,
                    tick,
                    ref nextProjectileSpawnIndex);
                TickEnemyEffects(enemies, tick);
                QueueBossSummons(enemies, pendingSummons);
                MoveEnemies(enemies, tick);
                SpawnSummons(
                    pendingSummons,
                    enemies,
                    timeline,
                    tick,
                    ref nextEnemyId);
                MoveProjectiles(projectiles, tick);
                FindHits(projectiles, enemies, hitCandidates);
                ResolveHits(
                    hitCandidates,
                    enemies,
                    timeline,
                    lastImpacts,
                    tick);
                RecordFrames(enemies, timeline, tick);
                RemoveCompleted(enemies, projectiles);

                if (nextWaveSpawnIndex >= wavePlan.Count && enemies.Count == 0)
                {
                    return timeline;
                }
            }

            throw new InvalidOperationException(
                $"Combat planning exceeded its deterministic horizon of {horizonTick} ticks.");
        }

        private long CalculatePlanningHorizon(IReadOnlyList<WaveSpawnOrder> wavePlan)
        {
            var trajectoryPlanner = new ProjectileHitPlanner(
                roadPaths,
                tickSeconds,
                projectileSpeed,
                ProjectileHitRadius);
            IReadOnlyList<EnemyTrajectoryPlan> trajectories =
                trajectoryPlanner.CreateWaveEnemyTrajectories(wavePlan);
            long lastMovementTick = 1L;
            for (int index = 0; index < trajectories.Count; index++)
            {
                lastMovementTick = Math.Max(
                    lastMovementTick,
                    trajectories[index].LastMovementTick);
            }

            float progressFraction = CalculateGuaranteedProgressFraction();
            return checked((long)Math.Ceiling(lastMovementTick / progressFraction) + 200L);
        }

        /// <summary>
        /// Smallest share of its own move speed that an enemy is guaranteed to keep once every
        /// bounded slow-down is stacked: lift can only hold it for its share of each lift cycle,
        /// and knockback can only claw back its capped fraction of the rest. Staying above zero
        /// is what makes planning terminate, and the horizon is sized from it so a legal but
        /// heavily controlled wave is planned rather than rejected.
        /// </summary>
        private float CalculateGuaranteedProgressFraction()
        {
            float longestLiftUptime = 0f;
            IReadOnlyList<ElementReactionDefinition> reactions = reactionCatalog.Definitions;
            for (int index = 0; index < reactions.Count; index++)
            {
                ElementReactionDefinition reaction = reactions[index];
                if (reaction == null || reaction.LiftDurationSeconds <= 0f)
                {
                    continue;
                }

                float cycleSeconds = reaction.LiftDurationSeconds + reaction.LiftImmunitySeconds;
                longestLiftUptime = Mathf.Max(
                    longestLiftUptime,
                    reaction.LiftDurationSeconds / cycleSeconds);
            }

            float fraction = (1f - longestLiftUptime) * (1f - maximumPushSpeedFraction);
            if (fraction <= 0f)
            {
                throw new InvalidOperationException(
                    "Combat rules allow an enemy to be halted indefinitely: lift uptime "
                    + $"{longestLiftUptime:P0} combined with a push ceiling of "
                    + $"{maximumPushSpeedFraction:P0} leaves no forward progress.");
            }

            return fraction;
        }

        private void SpawnWaveEnemies(
            IReadOnlyList<WaveSpawnOrder> wavePlan,
            List<ShadowEnemy> enemies,
            long tick,
            ref int nextSpawnIndex)
        {
            while (nextSpawnIndex < wavePlan.Count
                && SecondsToTick(wavePlan[nextSpawnIndex].TimeSeconds) <= tick)
            {
                WaveSpawnOrder order = wavePlan[nextSpawnIndex++];
                int routeIndex = roadPaths.GetRouteIndex(order.EnemyId);
                enemies.Add(new ShadowEnemy(
                    order.EnemyId,
                    order.Enemy,
                    roadPaths.Get(routeIndex),
                    routeIndex,
                    roadPaths.Get(routeIndex).Start,
                    1,
                    isSummoned: false,
                    reactionCatalog,
                    tickSeconds,
                    checked(tick + GetSpawnMovementDelayTicks())));
            }
        }

        private void SpawnProjectiles(
            IReadOnlyList<TowerProjectileSpawnOrder> projectilePlan,
            List<ShadowProjectile> projectiles,
            long tick,
            ref int nextSpawnIndex)
        {
            while (nextSpawnIndex < projectilePlan.Count
                && projectilePlan[nextSpawnIndex].SpawnTick <= tick)
            {
                TowerProjectileSpawnOrder order = projectilePlan[nextSpawnIndex++];
                towerNetworkManager.TryGetNodePosition(
                    order.Projectile.Target,
                    out TowerWorldPosition targetPosition);
                projectiles.Add(new ShadowProjectile(
                    order.SpawnTick,
                    order.Projectile,
                    ToVector3(targetPosition)));
            }
        }

        private void TickEnemyEffects(List<ShadowEnemy> enemies, long tick)
        {
            for (int index = 0; index < enemies.Count; index++)
            {
                ShadowEnemy enemy = enemies[index];
                enemy.PreviousPosition = enemy.Position;
                enemy.Removal = PlannedEnemyRemoval.None;
                RefillPushBudget(enemy);
                enemy.SkillCastCompletedThisTick = false;
                ExpireEffects(enemy, tick);
                UpdateSpeedSupport(enemy);
                if (!enemy.IsAlive || enemy.BurnDamagePerTick <= 0f
                    || tick < enemy.NextBurnTick || tick >= enemy.BurnEndTick)
                {
                    continue;
                }

                ApplyDamage(enemy, enemy.BurnDamagePerTick, isThermalShock: false);
                enemy.NextBurnTick += enemy.BurnIntervalTicks;
                if (!enemy.IsAlive)
                {
                    enemy.Removal = PlannedEnemyRemoval.Killed;
                }
            }
        }

        /// <summary>
        /// Tops the knockback budget back up by one tick's worth of allowance, capped at one
        /// second so an enemy that walked unharassed cannot be shoved a long way at once.
        /// </summary>
        private void RefillPushBudget(ShadowEnemy enemy)
        {
            float allowancePerSecond = enemy.Definition.BaseMoveSpeed * maximumPushSpeedFraction;
            enemy.PushBudgetMeters = Mathf.Min(
                allowancePerSecond,
                enemy.PushBudgetMeters + allowancePerSecond * tickSeconds);
        }

        private void ExpireEffects(ShadowEnemy enemy, long tick)
        {
            enemy.ElementReaction.Advance(tick);

            if (tick >= enemy.BurnEndTick)
            {
                enemy.BurnDamagePerTick = 0f;
            }

            enemy.RevealRemainingSeconds = Mathf.Max(
                0f,
                enemy.RevealRemainingSeconds - tickSeconds);
        }

        private void UpdateSpeedSupport(ShadowEnemy enemy)
        {
            if (!(enemy.Definition is SpeedSupportEnemyDefinition support))
            {
                return;
            }

            if (enemy.SupportActivationRemainingSeconds > 0f)
            {
                enemy.SupportActivationRemainingSeconds = Mathf.Max(
                    0f,
                    enemy.SupportActivationRemainingSeconds - tickSeconds);
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
                enemy.SkillCastRemainingSeconds - tickSeconds);
            if (enemy.SkillCastRemainingSeconds <= 0f)
            {
                enemy.SkillCastCompletedThisTick = true;
            }
        }

        private void QueueBossSummons(
            List<ShadowEnemy> enemies,
            List<ShadowSummon> pendingSummons)
        {
            pendingSummons.Clear();
            for (int index = 0; index < enemies.Count; index++)
            {
                ShadowEnemy boss = enemies[index];
                if (!boss.IsAlive
                    || !(boss.Definition is SummonerBossEnemyDefinition definition))
                {
                    continue;
                }

                int phaseIndex = FindSummonPhase(definition, boss.HealthFraction);
                if (phaseIndex != boss.SummonPhaseIndex)
                {
                    boss.SummonPhaseIndex = phaseIndex;
                    boss.SummonElapsedSeconds = 0f;
                }

                if (boss.SummonCastRemainingSeconds > 0f)
                {
                    boss.SummonCastRemainingSeconds = Mathf.Max(
                        0f,
                        boss.SummonCastRemainingSeconds - tickSeconds);
                    if (boss.SummonCastRemainingSeconds <= 0f)
                    {
                        boss.SkillCastCompletedThisTick = true;
                    }
                    boss.SkillCastRemainingSeconds = boss.SummonCastRemainingSeconds;
                    if (boss.SummonCastRemainingSeconds > 0f)
                    {
                        continue;
                    }

                    AddSummons(definition.SummonPhases[boss.SummonPhaseIndex], boss, pendingSummons);
                    continue;
                }

                SummonerBossEnemyDefinition.SummonPhase phase =
                    definition.SummonPhases[phaseIndex];
                boss.SummonElapsedSeconds += tickSeconds;
                while (boss.SummonElapsedSeconds >= phase.SummonIntervalSeconds)
                {
                    boss.SummonElapsedSeconds -= phase.SummonIntervalSeconds;
                    boss.SummonCastRemainingSeconds = definition.SummonSkillDurationSeconds;
                    boss.SkillCastRemainingSeconds = definition.SummonSkillDurationSeconds;
                    boss.SkillCastCompletedThisTick = true;
                    boss.SkillCastVersion++;
                    break;
                }
            }
        }

        private static void AddSummons(
            SummonerBossEnemyDefinition.SummonPhase phase,
            ShadowEnemy boss,
            List<ShadowSummon> pendingSummons)
        {
            for (int entryIndex = 0; entryIndex < phase.Entries.Count; entryIndex++)
            {
                SummonerBossEnemyDefinition.SummonedEnemyEntry entry = phase.Entries[entryIndex];
                for (int count = 0; count < entry.Count; count++)
                {
                    pendingSummons.Add(new ShadowSummon(
                        entry.Definition,
                        boss.Position,
                        boss.TargetPointIndex,
                        boss.RouteIndex));
                }
            }
        }

        private void MoveEnemies(List<ShadowEnemy> enemies, long tick)
        {
            for (int index = 0; index < enemies.Count; index++)
            {
                ShadowEnemy enemy = enemies[index];
                if (!enemy.IsAlive)
                {
                    continue;
                }

                if (tick < enemy.FirstMovementTick)
                {
                    enemy.PreviousPosition = enemy.Position;
                    continue;
                }

                float speedBonus = FindStrongestSpeedBonus(enemy, enemies);
                enemy.IsSpeedBuffed = speedBonus > 0f;

                if (tick < enemy.LiftEndTick)
                {
                    continue;
                }

                if (enemy.SkillCastRemainingSeconds > 0f || enemy.SkillCastCompletedThisTick)
                {
                    enemy.PreviousPosition = enemy.Position;
                    continue;
                }

                float speedMultiplier = 1f + speedBonus;
                float distance = enemy.Definition.BaseMoveSpeed * speedMultiplier * tickSeconds;
                Vector3 position = enemy.Position;
                int targetPointIndex = enemy.TargetPointIndex;
                bool reachedEnd = enemy.Route.Move(ref targetPointIndex, ref position, distance);
                enemy.Position = position;
                enemy.TargetPointIndex = targetPointIndex;
                if (reachedEnd)
                {
                    enemy.Removal = PlannedEnemyRemoval.Leaked;
                }
            }
        }

        private static float FindStrongestSpeedBonus(
            ShadowEnemy target,
            List<ShadowEnemy> enemies)
        {
            if (target.Definition.Rank == EnemyRank.Boss)
            {
                return 0f;
            }

            float strongest = 0f;
            for (int index = 0; index < enemies.Count; index++)
            {
                ShadowEnemy source = enemies[index];
                if (source == target || !source.IsAlive
                    || !source.IsSpeedAuraActive
                    || !(source.Definition is SpeedSupportEnemyDefinition support))
                {
                    continue;
                }

                if (!IsWithinRadiusXZ(
                    source.Position,
                    target.Position,
                    support.AuraRadiusMeters))
                {
                    continue;
                }

                float bonus = target.Definition.Rank == EnemyRank.MiniBoss
                    ? support.MiniBossSpeedBonusFraction
                    : support.RegularSpeedBonusFraction;
                strongest = Mathf.Max(strongest, bonus);
            }

            return strongest;
        }

        private void SpawnSummons(
            List<ShadowSummon> pendingSummons,
            List<ShadowEnemy> enemies,
            CombatTimeline timeline,
            long tick,
            ref long nextEnemyId)
        {
            for (int index = 0; index < pendingSummons.Count; index++)
            {
                ShadowSummon summon = pendingSummons[index];
                long enemyId = nextEnemyId++;
                enemies.Add(new ShadowEnemy(
                    enemyId,
                    summon.Definition,
                    roadPaths.Get(summon.RouteIndex),
                    summon.RouteIndex,
                    summon.Position,
                    summon.TargetPointIndex,
                    isSummoned: true,
                    reactionCatalog,
                    tickSeconds,
                    checked(tick + GetSpawnMovementDelayTicks())));
                timeline.Add(tick, new PlannedEnemySpawn(
                    enemyId,
                    summon.Definition,
                    summon.Position,
                    summon.TargetPointIndex,
                    summon.RouteIndex));
            }
        }

        private void MoveProjectiles(List<ShadowProjectile> projectiles, long tick)
        {
            for (int index = 0; index < projectiles.Count; index++)
            {
                ShadowProjectile projectile = projectiles[index];
                projectile.PreviousPosition = projectile.Position;
                projectile.MovementDurationSeconds = 0f;
                if (tick <= projectile.SpawnTick + projectile.LaunchDelayTicks)
                {
                    continue;
                }

                float distance = Vector3.Distance(projectile.Position, projectile.TargetPosition);
                if (distance <= float.Epsilon)
                {
                    projectile.HasEnded = true;
                    continue;
                }

                projectile.MovementDurationSeconds = Mathf.Min(tickSeconds, distance / projectileSpeed);
                projectile.Position = Vector3.MoveTowards(
                    projectile.Position,
                    projectile.TargetPosition,
                    projectileSpeed * tickSeconds);
                projectile.HasEnded = Vector3.Distance(
                    projectile.Position,
                    projectile.TargetPosition) <= float.Epsilon;
            }
        }

        private void FindHits(
            List<ShadowProjectile> projectiles,
            List<ShadowEnemy> enemies,
            List<ShadowHit> hits)
        {
            hits.Clear();
            for (int projectileIndex = 0; projectileIndex < projectiles.Count; projectileIndex++)
            {
                ShadowProjectile projectile = projectiles[projectileIndex];
                if (projectile.MovementDurationSeconds <= 0f)
                {
                    continue;
                }

                Vector3 projectileVelocity =
                    (projectile.Position - projectile.PreviousPosition)
                    / projectile.MovementDurationSeconds;
                for (int enemyIndex = 0; enemyIndex < enemies.Count; enemyIndex++)
                {
                    ShadowEnemy enemy = enemies[enemyIndex];
                    if (!enemy.IsAlive || enemy.Removal == PlannedEnemyRemoval.Leaked
                        || (enemy.IsHidden
                            && projectile.Payload.Kind != ProjectilePayloadKind.Water)
                        || projectile.HitEnemyIds.Contains(enemy.Id))
                    {
                        continue;
                    }

                    Vector3 enemyVelocity = (enemy.Position - enemy.PreviousPosition) / tickSeconds;
                    if (!TrajectoryHitCalculator.TryFindFirstIntersectionTimeXZ(
                        projectile.PreviousPosition,
                        projectileVelocity,
                        enemy.PreviousPosition,
                        enemyVelocity,
                        projectile.MovementDurationSeconds,
                        ProjectileHitRadius + enemy.Definition.BaseHitRadius,
                        out float hitTime))
                    {
                        continue;
                    }

                    hits.Add(new ShadowHit(
                        projectile,
                        enemy,
                        hitTime,
                        projectile.PreviousPosition + projectileVelocity * hitTime));
                }
            }

            hits.Sort(CompareHits);
        }

        private void ResolveHits(
            List<ShadowHit> hits,
            List<ShadowEnemy> enemies,
            CombatTimeline timeline,
            Dictionary<long, ProjectileImpactHistory> lastImpacts,
            long tick)
        {
            for (int index = 0; index < hits.Count; index++)
            {
                ShadowHit hit = hits[index];
                if (!hit.Enemy.IsAlive || !hit.Projectile.HitEnemyIds.Add(hit.Enemy.Id))
                {
                    continue;
                }

                ResolveDirectHit(hit, enemies, timeline, tick);
                if (ShouldPresentImpact(hit, lastImpacts, tick))
                {
                    timeline.Add(tick, new ProjectileImpactEvent(
                        hit.Projectile.ProjectileId,
                        hit.Position));
                }
            }
        }

        private void ResolveDirectHit(
            ShadowHit hit,
            List<ShadowEnemy> enemies,
            CombatTimeline timeline,
            long tick)
        {
            ShadowEnemy enemy = hit.Enemy;
            ProjectilePayload payload = hit.Projectile.Payload;
            if (payload.Kind == ProjectilePayloadKind.Water
                && enemy.Definition is StealthEnemyDefinition stealth)
            {
                enemy.RevealRemainingSeconds = stealth.RevealDurationSeconds;
            }

            if (payload.Kind == ProjectilePayloadKind.Wind)
            {
                ApplyPush(enemy, payload.PushDistanceMeters);
            }

            ApplyDamage(enemy, payload.Damage, isThermalShock: false);
            if (payload.Kind == ProjectilePayloadKind.Fire)
            {
                ApplyBurn(
                    enemy,
                    payload.BurnDamagePerTick,
                    payload.BurnTickIntervalSeconds,
                    payload.BurnDurationSeconds,
                    tick);
            }

            if (TryGetElement(payload.Kind, out ElementType incoming))
            {
                ApplyElement(enemy, incoming, hit.Position, enemies, timeline, tick);
            }

            if (!enemy.IsAlive)
            {
                enemy.Removal = PlannedEnemyRemoval.Killed;
            }
        }

        private void ApplyElement(
            ShadowEnemy enemy,
            ElementType incoming,
            Vector3 hitPosition,
            List<ShadowEnemy> enemies,
            CombatTimeline timeline,
            long tick)
        {
            if (!enemy.ElementReaction.TryReceive(
                incoming,
                1f,
                tick,
                out ElementReactionDefinition reaction))
            {
                return;
            }

            timeline.Add(tick, new PlannedReactionEvent(
                enemy.Id,
                reaction.ReactionId,
                reaction.Pair,
                hitPosition,
                reaction.BurnDurationSeconds));
            ApplyReaction(reaction, enemy, hitPosition, enemies, tick);
        }

        private void ApplyReaction(
            ElementReactionDefinition reaction,
            ShadowEnemy primary,
            Vector3 position,
            List<ShadowEnemy> enemies,
            long tick)
        {
            if (reaction.RadiusMeters <= 0f)
            {
                ApplyReactionToEnemy(reaction, primary, tick);
            }
            else
            {
                for (int index = 0; index < enemies.Count; index++)
                {
                    ShadowEnemy target = enemies[index];
                    if (target.IsAlive && !target.IsHidden && IsWithinRadiusXZ(
                        target.Position,
                        position,
                        reaction.RadiusMeters))
                    {
                        ApplyReactionToEnemy(reaction, target, tick);
                    }
                }
            }

        }

        private void ApplyReactionToEnemy(
            ElementReactionDefinition reaction,
            ShadowEnemy enemy,
            long tick)
        {
            switch (reaction.ReactionId)
            {
                case ElementReactionId.ThermalShock:
                    ApplyDamage(enemy, reaction.Damage, isThermalShock: true);
                    break;
                case ElementReactionId.Firestorm:
                    enemy.ElementReaction.ForceMark(
                        ElementType.Fire,
                        1f,
                        tick);
                    ApplyBurn(
                        enemy,
                        reaction.BurnDamagePerTick,
                        reaction.BurnTickIntervalSeconds,
                        reaction.BurnDurationSeconds,
                        tick);
                    break;
                case ElementReactionId.WaterLift:
                    ApplyLift(reaction, enemy, tick);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(reaction));
            }
        }

        /// <summary>
        /// Lifts a regular enemy, then makes it immune until the lift has worn off and its
        /// immunity window has elapsed. Without the window a chain that re-triggers faster
        /// than the lift lasts would hold the enemy airborne forever: it would never advance,
        /// never leak, and never die, because the lift itself deals no damage.
        /// </summary>
        private void ApplyLift(
            ElementReactionDefinition reaction,
            ShadowEnemy enemy,
            long tick)
        {
            if (enemy.Definition.Rank != EnemyRank.Regular
                || tick < enemy.LiftImmuneUntilTick
                || reaction.LiftDurationSeconds <= 0f)
            {
                return;
            }

            enemy.LiftStartTick = tick;
            enemy.LiftEndTick = tick + SecondsToDurationTicks(reaction.LiftDurationSeconds);
            enemy.LiftImmuneUntilTick =
                enemy.LiftEndTick + SecondsToDurationTicks(reaction.LiftImmunitySeconds);
            enemy.LiftPeakHeightMeters = reaction.LiftHeightMeters;
        }

        private void ApplyBurn(
            ShadowEnemy enemy,
            float damagePerTick,
            float tickIntervalSeconds,
            float durationSeconds,
            long tick)
        {
            if (damagePerTick <= 0f || tickIntervalSeconds <= 0f || durationSeconds <= 0f)
            {
                return;
            }

            int intervalTicks = SecondsToDurationTicks(tickIntervalSeconds);
            float incomingDps = damagePerTick / intervalTicks;
            float activeDps = enemy.BurnIntervalTicks > 0
                ? enemy.BurnDamagePerTick / enemy.BurnIntervalTicks
                : 0f;
            if (incomingDps < activeDps)
            {
                return;
            }

            enemy.BurnDamagePerTick = damagePerTick;
            enemy.BurnIntervalTicks = intervalTicks;
            enemy.NextBurnTick = tick + intervalTicks;
            enemy.BurnEndTick = tick + SecondsToDurationTicks(durationSeconds);
        }

        private static void ApplyDamage(
            ShadowEnemy enemy,
            float damage,
            bool isThermalShock)
        {
            if (damage <= 0f || !enemy.IsAlive)
            {
                return;
            }

            if (enemy.RemainingThermalShieldHits > 0)
            {
                if (isThermalShock)
                {
                    enemy.RemainingThermalShieldHits--;
                }

                return;
            }

            enemy.Health = Mathf.Max(0f, enemy.Health - damage);
            if (!enemy.IsAlive)
            {
                enemy.Removal = PlannedEnemyRemoval.Killed;
            }
        }

        private void ApplyPush(ShadowEnemy enemy, float distance)
        {
            // Knockback spends a budget that refills at a fraction of the enemy's own move
            // speed, so however many pushing towers fire at once they can never drag it
            // backwards faster than it walks forwards.
            float effectiveDistance = Mathf.Min(distance, enemy.PushBudgetMeters);
            if (effectiveDistance <= 0f)
            {
                return;
            }

            enemy.PushBudgetMeters -= effectiveDistance;

            Vector3 position = enemy.Position;
            int targetPointIndex = enemy.TargetPointIndex;
            while (effectiveDistance > 0f && targetPointIndex > 0)
            {
                Vector3 previousPoint = enemy.Route.GetPoint(targetPointIndex - 1);
                float distanceToPrevious = Vector3.Distance(position, previousPoint);
                if (distanceToPrevious <= float.Epsilon)
                {
                    targetPointIndex--;
                    continue;
                }

                float travel = Mathf.Min(effectiveDistance, distanceToPrevious);
                position = Vector3.MoveTowards(position, previousPoint, travel);
                effectiveDistance -= travel;
                if (travel >= distanceToPrevious)
                {
                    targetPointIndex--;
                }
            }

            enemy.Position = position;
            enemy.TargetPointIndex = Math.Max(1, targetPointIndex);
        }

        private void RecordFrames(
            List<ShadowEnemy> enemies,
            CombatTimeline timeline,
            long tick)
        {
            for (int index = 0; index < enemies.Count; index++)
            {
                ShadowEnemy enemy = enemies[index];
                timeline.Add(tick, new PlannedEnemyFrame(
                    enemy.Id,
                    enemy.PreviousPosition,
                    enemy.Position,
                    enemy.Health,
                    enemy.RevealRemainingSeconds,
                    enemy.TargetPointIndex,
                    enemy.ElementReaction.Phase,
                    enemy.ElementReaction.Element,
                    enemy.ElementReaction.GetRemainingSeconds(tick),
                    enemy.RemainingThermalShieldHits,
                    CalculateLiftHeight(enemy, tick),
                    enemy.SkillCastVersion,
                    enemy.IsSpeedBuffed,
                    enemy.Removal));
            }
        }

        /// <summary>
        /// Arc the enemy rides while it is held airborne: zero at both ends of the lift so it
        /// leaves and returns to the ground cleanly, peaking halfway through.
        /// </summary>
        private static float CalculateLiftHeight(ShadowEnemy enemy, long tick)
        {
            long duration = enemy.LiftEndTick - enemy.LiftStartTick;
            if (duration <= 0L || tick < enemy.LiftStartTick || tick >= enemy.LiftEndTick)
            {
                return 0f;
            }

            float progress = (float)(tick - enemy.LiftStartTick) / duration;
            return 4f * progress * (1f - progress) * enemy.LiftPeakHeightMeters;
        }

        private static void RemoveCompleted(
            List<ShadowEnemy> enemies,
            List<ShadowProjectile> projectiles)
        {
            for (int index = enemies.Count - 1; index >= 0; index--)
            {
                if (enemies[index].Removal != PlannedEnemyRemoval.None)
                {
                    enemies.RemoveAt(index);
                }
            }

            for (int index = projectiles.Count - 1; index >= 0; index--)
            {
                if (projectiles[index].HasEnded)
                {
                    projectiles.RemoveAt(index);
                }
            }
        }

        private bool ShouldPresentImpact(
            ShadowHit hit,
            Dictionary<long, ProjectileImpactHistory> lastImpacts,
            long tick)
        {
            if (lastImpacts.TryGetValue(
                hit.Projectile.ProjectileId,
                out ProjectileImpactHistory previous)
                && tick - previous.Tick <= ImpactMergeWindowTicks
                && Vector3.Distance(previous.Position, hit.Position)
                <= ImpactMergeDistanceMeters)
            {
                return false;
            }

            lastImpacts[hit.Projectile.ProjectileId] =
                new ProjectileImpactHistory(tick, hit.Position);
            return true;
        }

        private static int CompareHits(ShadowHit left, ShadowHit right)
        {
            int time = left.TimeSeconds.CompareTo(right.TimeSeconds);
            if (time != 0)
            {
                return time;
            }

            int projectile = left.Projectile.ProjectileId.CompareTo(
                right.Projectile.ProjectileId);
            return projectile != 0
                ? projectile
                : left.Enemy.Id.CompareTo(right.Enemy.Id);
        }

        private static bool TryGetElement(
            ProjectilePayloadKind kind,
            out ElementType element)
        {
            switch (kind)
            {
                case ProjectilePayloadKind.Fire:
                    element = ElementType.Fire;
                    return true;
                case ProjectilePayloadKind.Water:
                    element = ElementType.Water;
                    return true;
                case ProjectilePayloadKind.Wind:
                    element = ElementType.Wind;
                    return true;
                default:
                    element = default;
                    return false;
            }
        }

        private static bool IsWithinRadiusXZ(Vector3 first, Vector3 second, float radius)
        {
            float x = first.x - second.x;
            float z = first.z - second.z;
            return x * x + z * z <= radius * radius;
        }

        private int SecondsToDurationTicks(float seconds)
        {
            return Math.Max(1, Mathf.CeilToInt(seconds / tickSeconds));
        }

        private long SecondsToTick(float seconds)
        {
            return Math.Max(1L, (long)Math.Ceiling(seconds / tickSeconds - 0.000001d));
        }

        private long GetSpawnMovementDelayTicks() =>
            SecondsToTick(EnemySpawnPresentationTiming.SpawnMovementDelaySeconds);

        private static long FindNextEnemyId(IReadOnlyList<WaveSpawnOrder> wavePlan)
        {
            long next = 1L;
            for (int index = 0; index < wavePlan.Count; index++)
            {
                next = Math.Max(next, wavePlan[index].EnemyId + 1L);
            }

            return next;
        }

        private static int FindSummonPhase(
            SummonerBossEnemyDefinition definition,
            float healthFraction)
        {
            int selected = 0;
            for (int index = 1; index < definition.SummonPhases.Count; index++)
            {
                if (healthFraction > definition.SummonPhases[index].StartHealthFraction)
                {
                    break;
                }

                selected = index;
            }

            return selected;
        }

        private sealed class ShadowEnemy
        {
            public ShadowEnemy(
                long id,
                EnemyDefinition definition,
                RoadPath route,
                int routeIndex,
                Vector3 position,
                int targetPointIndex,
                bool isSummoned,
                ElementReactionCatalog reactionCatalog,
                float tickSeconds,
                long firstMovementTick)
            {
                Id = id;
                Definition = definition;
                Route = route ?? throw new ArgumentNullException(nameof(route));
                RouteIndex = routeIndex;
                Position = position;
                PreviousPosition = position;
                TargetPointIndex = targetPointIndex;
                Health = definition.BaseMaxHealth;
                IsSummoned = isSummoned;
                FirstMovementTick = firstMovementTick;
                ElementReaction = new EnemyElementReactionState(
                    reactionCatalog,
                    tickSeconds);
                RemainingThermalShieldHits = definition.ThermalShockHitsToBreakShield;
                SupportActivationRemainingSeconds = definition is SpeedSupportEnemyDefinition support
                    ? support.ActivationDelaySeconds
                    : 0f;
            }

            public long Id { get; }
            public EnemyDefinition Definition { get; }
            public RoadPath Route { get; }
            public int RouteIndex { get; }
            public bool IsSummoned { get; }
            public long FirstMovementTick { get; }
            public float Health { get; set; }
            public float HealthFraction => Health / Definition.BaseMaxHealth;
            public bool IsAlive => Health > 0f;
            public bool IsHidden => Definition is StealthEnemyDefinition
                && RevealRemainingSeconds <= 0f;
            public Vector3 PreviousPosition { get; set; }
            public Vector3 Position { get; set; }
            public int TargetPointIndex { get; set; }
            public float RevealRemainingSeconds { get; set; }
            public bool IsSpeedAuraActive { get; set; }
            public bool IsSpeedBuffed { get; set; }
            public int SkillCastVersion { get; set; }
            public EnemyElementReactionState ElementReaction { get; }
            public int RemainingThermalShieldHits { get; set; }
            public long LiftStartTick { get; set; }
            public long LiftEndTick { get; set; }
            public long LiftImmuneUntilTick { get; set; }
            public float LiftPeakHeightMeters { get; set; }
            public float PushBudgetMeters { get; set; }
            public float BurnDamagePerTick { get; set; }
            public int BurnIntervalTicks { get; set; }
            public long NextBurnTick { get; set; }
            public long BurnEndTick { get; set; }
            public int SummonPhaseIndex { get; set; } = -1;
            public float SummonElapsedSeconds { get; set; }
            public float SummonCastRemainingSeconds { get; set; }
            public float SupportActivationRemainingSeconds { get; set; }
            public float SkillCastRemainingSeconds { get; set; }
            public bool SkillCastCompletedThisTick { get; set; }
            public PlannedEnemyRemoval Removal { get; set; }
        }

        private sealed class ShadowProjectile
        {
            public ShadowProjectile(
                long spawnTick,
                TowerProjectileSnapshot snapshot,
                Vector3 targetPosition)
            {
                SpawnTick = spawnTick;
                ProjectileId = snapshot.ProjectileId;
                Payload = snapshot.Payload;
                Position = ToVector3(snapshot.Position);
                PreviousPosition = Position;
                LaunchDelayTicks = snapshot.LaunchDelayTicks;
                Target = snapshot.Target;
                TargetPosition = targetPosition;
            }

            public long SpawnTick { get; }
            public long ProjectileId { get; }
            public ProjectilePayload Payload { get; }
            public int LaunchDelayTicks { get; }
            public TowerNodeId Target { get; }
            public Vector3 PreviousPosition { get; set; }
            public Vector3 Position { get; set; }
            public Vector3 TargetPosition { get; set; }
            public float MovementDurationSeconds { get; set; }
            public bool HasEnded { get; set; }
            public HashSet<long> HitEnemyIds { get; } = new HashSet<long>();

        }

        private static Vector3 ToVector3(TowerWorldPosition value)
        {
            return new Vector3(value.X, value.Y, value.Z);
        }

        private readonly struct ShadowHit
        {
            public ShadowHit(
                ShadowProjectile projectile,
                ShadowEnemy enemy,
                float timeSeconds,
                Vector3 position)
            {
                Projectile = projectile;
                Enemy = enemy;
                TimeSeconds = timeSeconds;
                Position = position;
            }

            public ShadowProjectile Projectile { get; }
            public ShadowEnemy Enemy { get; }
            public float TimeSeconds { get; }
            public Vector3 Position { get; }
        }

        private readonly struct ShadowSummon
        {
            public ShadowSummon(
                EnemyDefinition definition,
                Vector3 position,
                int targetPointIndex,
                int routeIndex)
            {
                Definition = definition;
                Position = position;
                TargetPointIndex = targetPointIndex;
                RouteIndex = routeIndex;
            }

            public EnemyDefinition Definition { get; }
            public Vector3 Position { get; }
            public int TargetPointIndex { get; }
            public int RouteIndex { get; }
        }

    }
}
