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
        private const float HiddenDamageMultiplier = 0.5f;
        private const long ImpactMergeWindowTicks = 4L;
        private const float ImpactMergeDistanceMeters = 1.25f;

        private readonly TowerNetworkManager towerNetworkManager;
        private readonly RoadPath roadPath;
        private readonly ElementReactionCatalog reactionCatalog;
        private readonly float tickSeconds;
        private readonly float projectileSpeed;

        public CombatTimelinePlanner(
            TowerNetworkManager towerNetworkManager,
            RoadPath roadPath,
            ElementReactionCatalog reactionCatalog)
        {
            this.towerNetworkManager = towerNetworkManager;
            this.roadPath = roadPath;
            this.reactionCatalog = reactionCatalog;
            tickSeconds = towerNetworkManager.TickSeconds;
            projectileSpeed = towerNetworkManager.ProjectileSpeedMetersPerSecond;
        }

        internal CombatTimeline Create(IReadOnlyList<WaveSpawnOrder> wavePlan)
        {
            long horizonTick = CalculatePlanningHorizon(wavePlan);
            IReadOnlyList<TowerProjectileSpawnOrder> projectilePlan =
                towerNetworkManager.EnsureProjectileSpawnPlanThrough(horizonTick);
            var timeline = new CombatTimeline();
            var enemies = new List<ShadowEnemy>();
            var projectiles = new List<ShadowProjectile>();
            var fields = new List<ShadowField>();
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
                TickFields(fields, enemies, tick);
                TickEnemyEffects(enemies, tick);
                QueueBossSummons(enemies, pendingSummons);
                MoveEnemies(enemies);
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
                    fields,
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
                roadPath,
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

            return checked(lastMovementTick * 4L + 200L);
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
                enemies.Add(new ShadowEnemy(
                    order.EnemyId,
                    order.Enemy,
                    roadPath.Start,
                    1,
                    isSummoned: false,
                    reactionCatalog,
                    tickSeconds));
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

        private void TickFields(
            List<ShadowField> fields,
            List<ShadowEnemy> enemies,
            long tick)
        {
            for (int fieldIndex = fields.Count - 1; fieldIndex >= 0; fieldIndex--)
            {
                ShadowField field = fields[fieldIndex];
                if (tick >= field.EndTick)
                {
                    fields.RemoveAt(fieldIndex);
                    continue;
                }

                for (int enemyIndex = 0; enemyIndex < enemies.Count; enemyIndex++)
                {
                    ShadowEnemy enemy = enemies[enemyIndex];
                    if (!enemy.IsAlive || !IsWithinRadiusXZ(enemy.Position, field.Position, field.Radius))
                    {
                        continue;
                    }

                    ApplySlow(enemy, field.SlowFraction, tick + 1L);
                    ApplyResistanceReduction(
                        enemy,
                        field.PhysicalResistanceReductionPoints,
                        0f,
                        tick + 1L);
                }
            }
        }

        private void TickEnemyEffects(List<ShadowEnemy> enemies, long tick)
        {
            for (int index = 0; index < enemies.Count; index++)
            {
                ShadowEnemy enemy = enemies[index];
                enemy.PreviousPosition = enemy.Position;
                enemy.Removal = PlannedEnemyRemoval.None;
                ExpireEffects(enemy, tick);
                if (!enemy.IsAlive || enemy.BurnDamagePerTick <= 0f
                    || tick < enemy.NextBurnTick || tick >= enemy.BurnEndTick)
                {
                    continue;
                }

                ResolvedDamage burnDamage = EnemyDamageResolver.Resolve(
                    new DamageChannels(0f, enemy.BurnDamagePerTick),
                    enemy.Definition,
                    -enemy.PhysicalResistanceReductionPoints,
                    -enemy.MagicResistanceReductionPoints);
                enemy.Health = Mathf.Max(0f, enemy.Health - burnDamage.Total);
                enemy.NextBurnTick += enemy.BurnIntervalTicks;
                if (!enemy.IsAlive)
                {
                    enemy.Removal = PlannedEnemyRemoval.Killed;
                }
            }
        }

        private void ExpireEffects(ShadowEnemy enemy, long tick)
        {
            enemy.ElementReaction.Advance(tick);

            if (tick >= enemy.SlowEndTick)
            {
                enemy.SlowFraction = 0f;
            }

            if (tick >= enemy.PhysicalResistanceReductionEndTick)
            {
                enemy.PhysicalResistanceReductionPoints = 0f;
            }

            if (tick >= enemy.MagicResistanceReductionEndTick)
            {
                enemy.MagicResistanceReductionPoints = 0f;
            }

            if (tick >= enemy.BurnEndTick)
            {
                enemy.BurnDamagePerTick = 0f;
            }

            enemy.RevealRemainingSeconds = Mathf.Max(
                0f,
                enemy.RevealRemainingSeconds - tickSeconds);
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

                SummonerBossEnemyDefinition.SummonPhase phase =
                    definition.SummonPhases[phaseIndex];
                boss.SummonElapsedSeconds += tickSeconds;
                while (boss.SummonElapsedSeconds >= phase.SummonIntervalSeconds)
                {
                    boss.SummonElapsedSeconds -= phase.SummonIntervalSeconds;
                    for (int entryIndex = 0; entryIndex < phase.Entries.Count; entryIndex++)
                    {
                        SummonerBossEnemyDefinition.SummonedEnemyEntry entry =
                            phase.Entries[entryIndex];
                        for (int count = 0; count < entry.Count; count++)
                        {
                            pendingSummons.Add(new ShadowSummon(
                                entry.Definition,
                                boss.Position,
                                boss.TargetPointIndex));
                        }
                    }
                }
            }
        }

        private void MoveEnemies(List<ShadowEnemy> enemies)
        {
            for (int index = 0; index < enemies.Count; index++)
            {
                ShadowEnemy enemy = enemies[index];
                if (!enemy.IsAlive)
                {
                    continue;
                }

                float speedBonus = FindStrongestSpeedBonus(enemy, enemies);
                float speedMultiplier = (1f + speedBonus) * (1f - enemy.SlowFraction);
                float distance = enemy.Definition.BaseMoveSpeed * speedMultiplier * tickSeconds;
                Vector3 position = enemy.Position;
                int targetPointIndex = enemy.TargetPointIndex;
                bool reachedEnd = roadPath.Move(ref targetPointIndex, ref position, distance);
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
                    summon.Position,
                    summon.TargetPointIndex,
                    isSummoned: true,
                    reactionCatalog,
                    tickSeconds));
                timeline.Add(tick, new PlannedEnemySpawn(
                    enemyId,
                    summon.Definition,
                    summon.Position,
                    summon.TargetPointIndex));
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
            List<ShadowField> fields,
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

                ResolveDirectHit(hit, enemies, fields, timeline, tick);
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
            List<ShadowField> fields,
            CombatTimeline timeline,
            long tick)
        {
            ShadowEnemy enemy = hit.Enemy;
            bool wasHidden = enemy.IsHidden;
            ResolvedDamage directDamage = EnemyDamageResolver.Resolve(
                hit.Projectile.Payload.DamageChannels,
                enemy.Definition,
                -enemy.PhysicalResistanceReductionPoints,
                -enemy.MagicResistanceReductionPoints);
            float directMultiplier = wasHidden ? HiddenDamageMultiplier : 1f;
            enemy.Health = Mathf.Max(0f, enemy.Health - directDamage.Total * directMultiplier);
            if (enemy.Definition is StealthEnemyDefinition stealth)
            {
                enemy.RevealRemainingSeconds = stealth.RevealDurationSeconds;
            }

            if (!wasHidden && TryGetElement(hit.Projectile.Payload.Kind, out ElementType incoming))
            {
                ApplyElement(enemy, incoming, hit.Position, enemies, fields, timeline, tick);
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
            List<ShadowField> fields,
            CombatTimeline timeline,
            long tick)
        {
            if (!enemy.ElementReaction.TryReceive(
                incoming,
                enemy.Definition.ElementStatusEffectMultiplier,
                tick,
                out ElementReactionDefinition reaction))
            {
                return;
            }

            timeline.Add(tick, new PlannedReactionEvent(
                enemy.Id,
                reaction.ReactionId,
                reaction.Pair,
                hitPosition));
            ApplyReaction(reaction, enemy, hitPosition, enemies, fields, tick);
        }

        private void ApplyReaction(
            ElementReactionDefinition reaction,
            ShadowEnemy primary,
            Vector3 position,
            List<ShadowEnemy> enemies,
            List<ShadowField> fields,
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
                    if (target.IsAlive && IsWithinRadiusXZ(
                        target.Position,
                        position,
                        reaction.RadiusMeters))
                    {
                        ApplyReactionToEnemy(reaction, target, tick);
                    }
                }
            }

            if (reaction.CreatesField)
            {
                fields.Add(new ShadowField(
                    position,
                    reaction.RadiusMeters,
                    reaction.SlowStrengthFraction,
                    reaction.PhysicalResistanceReductionPoints,
                    tick + SecondsToDurationTicks(
                        reaction.ResistanceReductionDurationSeconds)));
            }
        }

        private void ApplyReactionToEnemy(
            ElementReactionDefinition reaction,
            ShadowEnemy enemy,
            long tick)
        {
            bool hidden = enemy.IsHidden;
            ResolvedDamage damage = EnemyDamageResolver.Resolve(
                new DamageChannels(reaction.PhysicalDamage, reaction.MagicDamage),
                enemy.Definition,
                -enemy.PhysicalResistanceReductionPoints,
                -enemy.MagicResistanceReductionPoints);
            enemy.Health = Mathf.Max(
                0f,
                enemy.Health - damage.Total * (hidden ? HiddenDamageMultiplier : 1f));
            if (hidden || !enemy.IsAlive)
            {
                if (!enemy.IsAlive)
                {
                    enemy.Removal = PlannedEnemyRemoval.Killed;
                }

                return;
            }

            ApplyBurn(enemy, reaction, tick);
            ApplyPush(enemy, reaction.PushDistanceMeters);
            if (!reaction.CreatesField)
            {
                ApplyReactionSlow(enemy, reaction, tick);
                ApplyResistanceReduction(
                    enemy,
                    reaction.PhysicalResistanceReductionPoints,
                    reaction.MagicResistanceReductionPoints,
                    tick + SecondsToDurationTicks(
                        reaction.ResistanceReductionDurationSeconds));
            }
        }

        private void ApplyReactionSlow(
            ShadowEnemy enemy,
            ElementReactionDefinition reaction,
            long tick)
        {
            float duration = reaction.SlowDurationSeconds
                * enemy.Definition.SlowDurationMultiplier;
            if (duration > 0f)
            {
                ApplySlow(
                    enemy,
                    reaction.SlowStrengthFraction,
                    tick + SecondsToDurationTicks(duration));
            }
        }

        private void ApplyBurn(
            ShadowEnemy enemy,
            ElementReactionDefinition reaction,
            long tick)
        {
            if (reaction.BurnDamagePerTick <= 0f)
            {
                return;
            }

            int intervalTicks = SecondsToDurationTicks(reaction.BurnTickIntervalSeconds);
            float incomingDps = reaction.BurnDamagePerTick / intervalTicks;
            float activeDps = enemy.BurnIntervalTicks > 0
                ? enemy.BurnDamagePerTick / enemy.BurnIntervalTicks
                : 0f;
            if (incomingDps < activeDps)
            {
                return;
            }

            enemy.BurnDamagePerTick = reaction.BurnDamagePerTick;
            enemy.BurnIntervalTicks = intervalTicks;
            enemy.NextBurnTick = tick + intervalTicks;
            enemy.BurnEndTick = tick + SecondsToDurationTicks(reaction.BurnDurationSeconds);
        }

        private void ApplySlow(ShadowEnemy enemy, float strength, long endTick)
        {
            if (strength <= 0f || enemy.Definition.Rank == EnemyRank.Boss)
            {
                return;
            }

            float effectiveStrength = Mathf.Min(
                reactionCatalog.MaximumSlowFraction,
                strength * enemy.Definition.SlowStrengthMultiplier);
            if (effectiveStrength < enemy.SlowFraction)
            {
                return;
            }

            enemy.SlowFraction = effectiveStrength;
            enemy.SlowEndTick = Math.Max(enemy.SlowEndTick, endTick);
        }

        private void ApplyPush(ShadowEnemy enemy, float distance)
        {
            float effectiveDistance = distance * enemy.Definition.PushDistanceMultiplier;
            if (effectiveDistance <= 0f || enemy.Definition.Rank == EnemyRank.Boss)
            {
                return;
            }

            Vector3 position = enemy.Position;
            int targetPointIndex = enemy.TargetPointIndex;
            while (effectiveDistance > 0f && targetPointIndex > 0)
            {
                Vector3 previousPoint = roadPath.GetPoint(targetPointIndex - 1);
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

        private static void ApplyResistanceReduction(
            ShadowEnemy enemy,
            float physicalPoints,
            float magicPoints,
            long endTick)
        {
            if (physicalPoints >= enemy.PhysicalResistanceReductionPoints)
            {
                enemy.PhysicalResistanceReductionPoints = physicalPoints;
                enemy.PhysicalResistanceReductionEndTick = Math.Max(
                    enemy.PhysicalResistanceReductionEndTick,
                    endTick);
            }

            if (magicPoints >= enemy.MagicResistanceReductionPoints)
            {
                enemy.MagicResistanceReductionPoints = magicPoints;
                enemy.MagicResistanceReductionEndTick = Math.Max(
                    enemy.MagicResistanceReductionEndTick,
                    endTick);
            }
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
                    enemy.SlowFraction,
                    enemy.PhysicalResistanceReductionPoints,
                    enemy.MagicResistanceReductionPoints,
                    enemy.Removal));
            }
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
                case ProjectilePayloadKind.Earth:
                    element = ElementType.Earth;
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
                Vector3 position,
                int targetPointIndex,
                bool isSummoned,
                ElementReactionCatalog reactionCatalog,
                float tickSeconds)
            {
                Id = id;
                Definition = definition;
                Position = position;
                PreviousPosition = position;
                TargetPointIndex = targetPointIndex;
                Health = definition.BaseMaxHealth;
                IsSummoned = isSummoned;
                ElementReaction = new EnemyElementReactionState(
                    reactionCatalog,
                    tickSeconds);
            }

            public long Id { get; }
            public EnemyDefinition Definition { get; }
            public bool IsSummoned { get; }
            public float Health { get; set; }
            public float HealthFraction => Health / Definition.BaseMaxHealth;
            public bool IsAlive => Health > 0f;
            public bool IsHidden => Definition is StealthEnemyDefinition
                && RevealRemainingSeconds <= 0f;
            public Vector3 PreviousPosition { get; set; }
            public Vector3 Position { get; set; }
            public int TargetPointIndex { get; set; }
            public float RevealRemainingSeconds { get; set; }
            public EnemyElementReactionState ElementReaction { get; }
            public float SlowFraction { get; set; }
            public long SlowEndTick { get; set; }
            public float PhysicalResistanceReductionPoints { get; set; }
            public long PhysicalResistanceReductionEndTick { get; set; }
            public float MagicResistanceReductionPoints { get; set; }
            public long MagicResistanceReductionEndTick { get; set; }
            public float BurnDamagePerTick { get; set; }
            public int BurnIntervalTicks { get; set; }
            public long NextBurnTick { get; set; }
            public long BurnEndTick { get; set; }
            public int SummonPhaseIndex { get; set; } = -1;
            public float SummonElapsedSeconds { get; set; }
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
                int targetPointIndex)
            {
                Definition = definition;
                Position = position;
                TargetPointIndex = targetPointIndex;
            }

            public EnemyDefinition Definition { get; }
            public Vector3 Position { get; }
            public int TargetPointIndex { get; }
        }

        private readonly struct ShadowField
        {
            public ShadowField(
                Vector3 position,
                float radius,
                float slowFraction,
                float physicalResistanceReductionPoints,
                long endTick)
            {
                Position = position;
                Radius = radius;
                SlowFraction = slowFraction;
                PhysicalResistanceReductionPoints = physicalResistanceReductionPoints;
                EndTick = endTick;
            }

            public Vector3 Position { get; }
            public float Radius { get; }
            public float SlowFraction { get; }
            public float PhysicalResistanceReductionPoints { get; }
            public long EndTick { get; }
        }
    }
}
