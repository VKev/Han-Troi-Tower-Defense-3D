using System;
using System.Collections.Generic;
using TowerDefense3D.Towers;
using TowerDefense3D.Waves;

namespace TowerDefense3D.Enemies
{
    public sealed class ProjectileHitSystem : IDisposable
    {
        private const float ProjectileHitRadius = 0.2f;
        private readonly TowerNetworkManager towerNetworkManager;
        private readonly EnemySystem enemySystem;
        private readonly WaveSystem waveSystem;
        private readonly ProjectileHitPlanner planner;
        private readonly Dictionary<long, EnemyTrajectoryPlan> enemyTrajectories =
            new Dictionary<long, EnemyTrajectoryPlan>();
        private readonly Dictionary<long, List<ScheduledProjectileHit>> scheduledHitsByTick =
            new Dictionary<long, List<ScheduledProjectileHit>>();
        private readonly Dictionary<long, List<long>> projectileEndIdsByTick =
            new Dictionary<long, List<long>>();
        private readonly Dictionary<long, HashSet<long>> hitEnemyIdsByProjectile =
            new Dictionary<long, HashSet<long>>();
        private readonly List<ScheduledProjectileHit> scheduledHitBuffer =
            new List<ScheduledProjectileHit>();
        private readonly HashSet<int> presentedImpactGroupIds = new HashSet<int>();
        private readonly Dictionary<long, ProjectileImpactHistory> lastPresentedImpacts =
            new Dictionary<long, ProjectileImpactHistory>();
        private readonly List<EnemySnapshot> enemySnapshots = new List<EnemySnapshot>();
        private readonly List<TowerProjectileSnapshot> projectileSnapshots =
            new List<TowerProjectileSnapshot>();
        private readonly List<EnemyTrajectorySeed> enemySeeds = new List<EnemyTrajectorySeed>();
        private readonly HashSet<long> activeProjectileIds = new HashSet<long>();
        private readonly List<long> staleProjectileIds = new List<long>();
        private IReadOnlyList<WaveSpawnOrder> currentWavePlan = Array.Empty<WaveSpawnOrder>();
        private IReadOnlyList<TowerProjectileSpawnOrder> projectileSpawnPlan =
            Array.Empty<TowerProjectileSpawnOrder>();
        private bool requiresTrajectoryRebuild;
        private bool isDisposed;

        internal event Action<ProjectileImpactEvent> ProjectileImpacted;

        public ProjectileHitSystem(
            TowerNetworkManager towerNetworkManager,
            EnemySystem enemySystem,
            WaveSystem waveSystem,
            RoadPath roadPath)
        {
            this.towerNetworkManager = towerNetworkManager
                ?? throw new ArgumentNullException(nameof(towerNetworkManager));
            this.enemySystem = enemySystem ?? throw new ArgumentNullException(nameof(enemySystem));
            this.waveSystem = waveSystem ?? throw new ArgumentNullException(nameof(waveSystem));
            planner = new ProjectileHitPlanner(
                roadPath,
                towerNetworkManager.TickSeconds,
                towerNetworkManager.ProjectileSpeedMetersPerSecond,
                ProjectileHitRadius);

            waveSystem.WavePlanCreated += HandleWavePlanCreated;
            enemySystem.EnemySpawned += HandleEnemySpawned;
            enemySystem.EnemyKilled += HandleEnemyRemoved;
            enemySystem.EnemyLeaked += HandleEnemyRemoved;
        }

        public void Step()
        {
            long currentTick = towerNetworkManager.CurrentTick;
            ResolveScheduledHits(currentTick);
            ReleaseFinishedProjectilePlans(currentTick);
            if (requiresTrajectoryRebuild)
            {
                RebuildFutureTrajectories(currentTick);
            }
        }

        public void Reset()
        {
            currentWavePlan = Array.Empty<WaveSpawnOrder>();
            projectileSpawnPlan = Array.Empty<TowerProjectileSpawnOrder>();
            enemyTrajectories.Clear();
            scheduledHitsByTick.Clear();
            projectileEndIdsByTick.Clear();
            hitEnemyIdsByProjectile.Clear();
            scheduledHitBuffer.Clear();
            presentedImpactGroupIds.Clear();
            lastPresentedImpacts.Clear();
            enemySnapshots.Clear();
            projectileSnapshots.Clear();
            enemySeeds.Clear();
            activeProjectileIds.Clear();
            staleProjectileIds.Clear();
            requiresTrajectoryRebuild = false;
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            waveSystem.WavePlanCreated -= HandleWavePlanCreated;
            enemySystem.EnemySpawned -= HandleEnemySpawned;
            enemySystem.EnemyKilled -= HandleEnemyRemoved;
            enemySystem.EnemyLeaked -= HandleEnemyRemoved;
            Reset();
        }

        private void HandleWavePlanCreated(IReadOnlyList<WaveSpawnOrder> plan)
        {
            Reset();
            currentWavePlan = plan;
            AddEnemyTrajectories(planner.CreateWaveEnemyTrajectories(plan));
            EnsureProjectileSpawnPlan(towerNetworkManager.CurrentTick);
            RebuildProjectileHitSchedule(towerNetworkManager.CurrentTick);
        }

        private void HandleEnemySpawned(EnemySnapshot snapshot)
        {
            if (!enemyTrajectories.ContainsKey(snapshot.EnemyId))
            {
                requiresTrajectoryRebuild = true;
            }
        }

        private void HandleEnemyRemoved(EnemySnapshot snapshot)
        {
            enemyTrajectories.Remove(snapshot.EnemyId);
            if (snapshot.Definition is SpeedSupportEnemyDefinition)
            {
                requiresTrajectoryRebuild = true;
            }
        }

        private void RebuildFutureTrajectories(long currentTick)
        {
            CreateRemainingEnemySeeds(currentTick);
            enemyTrajectories.Clear();
            AddEnemyTrajectories(planner.CreateEnemyTrajectories(enemySeeds));
            EnsureProjectileSpawnPlan(currentTick);
            RebuildProjectileHitSchedule(currentTick);
            requiresTrajectoryRebuild = false;
        }

        private void EnsureProjectileSpawnPlan(long currentTick)
        {
            long planEndTick = currentTick;
            foreach (EnemyTrajectoryPlan trajectory in enemyTrajectories.Values)
            {
                planEndTick = Math.Max(planEndTick, trajectory.LastMovementTick);
            }

            projectileSpawnPlan =
                towerNetworkManager.EnsureProjectileSpawnPlanThrough(planEndTick);
        }

        private void RebuildProjectileHitSchedule(long currentTick)
        {
            towerNetworkManager.CopyProjectileSnapshotTo(projectileSnapshots);
            scheduledHitsByTick.Clear();
            projectileEndIdsByTick.Clear();
            scheduledHitBuffer.Clear();
            presentedImpactGroupIds.Clear();
            PruneProjectileHitHistory();

            for (int index = 0; index < projectileSnapshots.Count; index++)
            {
                AddProjectileTrajectory(projectileSnapshots[index], currentTick);
            }

            for (int index = 0; index < projectileSpawnPlan.Count; index++)
            {
                TowerProjectileSpawnOrder order = projectileSpawnPlan[index];
                if (order.SpawnTick > currentTick)
                {
                    AddProjectileTrajectory(order.Projectile, order.SpawnTick);
                }
            }

            BuildScheduledHitPlan();
        }

        private void AddProjectileTrajectory(
            TowerProjectileSnapshot projectile,
            long creationTick)
        {
            ProjectileTrajectoryPlan trajectory = CreateProjectileTrajectory(
                projectile,
                creationTick);
            AddProjectileEnd(trajectory);
            foreach (EnemyTrajectoryPlan enemyTrajectory in enemyTrajectories.Values)
            {
                ScheduleHit(trajectory, enemyTrajectory);
            }
        }

        private void CreateRemainingEnemySeeds(long currentTick)
        {
            enemySeeds.Clear();
            enemySystem.CopySnapshotsTo(enemySnapshots);
            for (int index = 0; index < enemySnapshots.Count; index++)
            {
                EnemySnapshot snapshot = enemySnapshots[index];
                if (!enemySystem.TryGetEnemy(snapshot.EnemyId, out EnemyInstance enemy))
                {
                    continue;
                }

                enemySeeds.Add(new EnemyTrajectorySeed(
                    enemy.Id,
                    enemy.Definition,
                    enemy.Position,
                    enemy.TargetPointIndex,
                    currentTick + 1L));
            }

            for (int index = 0; index < currentWavePlan.Count; index++)
            {
                WaveSpawnOrder order = currentWavePlan[index];
                long firstMovementTick = planner.GetFirstMovementTick(order.TimeSeconds);
                if (firstMovementTick <= currentTick)
                {
                    continue;
                }

                enemySeeds.Add(new EnemyTrajectorySeed(
                    order.EnemyId,
                    order.Enemy,
                    planner.RoadStart,
                    1,
                    firstMovementTick));
            }
        }

        private ProjectileTrajectoryPlan CreateProjectileTrajectory(
            TowerProjectileSnapshot projectile,
            long creationTick)
        {
            if (!towerNetworkManager.TryGetNodePosition(
                projectile.Target,
                out TowerWorldPosition targetPosition))
            {
                throw new InvalidOperationException(
                    $"Projectile target '{projectile.Target}' is not registered.");
            }

            return planner.CreateProjectileTrajectory(projectile, targetPosition, creationTick);
        }

        private void AddEnemyTrajectories(IReadOnlyList<EnemyTrajectoryPlan> trajectories)
        {
            for (int index = 0; index < trajectories.Count; index++)
            {
                EnemyTrajectoryPlan trajectory = trajectories[index];
                enemyTrajectories.Add(trajectory.EnemyId, trajectory);
            }
        }

        private void ScheduleHit(
            ProjectileTrajectoryPlan projectile,
            EnemyTrajectoryPlan enemy)
        {
            if (hitEnemyIdsByProjectile.TryGetValue(
                projectile.ProjectileId,
                out HashSet<long> hitEnemyIds)
                && hitEnemyIds.Contains(enemy.EnemyId))
            {
                return;
            }

            if (!planner.TryCreateScheduledHit(projectile, enemy, out ScheduledProjectileHit hit))
            {
                return;
            }

            scheduledHitBuffer.Add(hit);
        }

        private void ResolveScheduledHits(long currentTick)
        {
            if (!scheduledHitsByTick.TryGetValue(
                currentTick,
                out List<ScheduledProjectileHit> hits))
            {
                return;
            }

            for (int index = 0; index < hits.Count; index++)
            {
                ScheduledProjectileHit hit = hits[index];
                if (!enemySystem.TryGetEnemy(hit.EnemyId, out EnemyInstance enemy))
                {
                    continue;
                }

                HashSet<long> hitEnemyIds = GetHitEnemyIds(hit.ProjectileId);
                if (!hitEnemyIds.Add(hit.EnemyId))
                {
                    continue;
                }

                ApplyHit(hit.Payload, enemy);
                if (ShouldPresentImpact(hit))
                {
                    ProjectileImpacted?.Invoke(new ProjectileImpactEvent(
                        hit.ProjectileId,
                        hit.Position));
                }
            }

            scheduledHitsByTick.Remove(currentTick);
        }

        private void AddProjectileEnd(ProjectileTrajectoryPlan projectile)
        {
            if (!projectileEndIdsByTick.TryGetValue(
                projectile.LastMovementTick,
                out List<long> projectileIds))
            {
                projectileIds = new List<long>();
                projectileEndIdsByTick.Add(projectile.LastMovementTick, projectileIds);
            }

            projectileIds.Add(projectile.ProjectileId);
        }

        private void ReleaseFinishedProjectilePlans(long currentTick)
        {
            if (!projectileEndIdsByTick.TryGetValue(currentTick, out List<long> projectileIds))
            {
                return;
            }

            for (int index = 0; index < projectileIds.Count; index++)
            {
                long projectileId = projectileIds[index];
                hitEnemyIdsByProjectile.Remove(projectileId);
                lastPresentedImpacts.Remove(projectileId);
            }

            projectileEndIdsByTick.Remove(currentTick);
        }

        private void PruneProjectileHitHistory()
        {
            activeProjectileIds.Clear();
            for (int index = 0; index < projectileSnapshots.Count; index++)
            {
                activeProjectileIds.Add(projectileSnapshots[index].ProjectileId);
            }

            staleProjectileIds.Clear();
            foreach (long projectileId in hitEnemyIdsByProjectile.Keys)
            {
                if (!activeProjectileIds.Contains(projectileId))
                {
                    staleProjectileIds.Add(projectileId);
                }
            }

            for (int index = 0; index < staleProjectileIds.Count; index++)
            {
                hitEnemyIdsByProjectile.Remove(staleProjectileIds[index]);
            }
        }

        private void ApplyHit(ProjectilePayload payload, EnemyInstance enemy)
        {
            enemySystem.RevealFromDirectHit(enemy.Id);
            ResolvedDamage damage = EnemyDamageResolver.Resolve(
                payload.DamageChannels,
                enemy.Definition);
            enemySystem.ApplyDamage(enemy.Id, damage.Total);
        }

        private void BuildScheduledHitPlan()
        {
            planner.AssignImpactGroups(scheduledHitBuffer, lastPresentedImpacts);
            for (int index = 0; index < scheduledHitBuffer.Count; index++)
            {
                AddScheduledHit(scheduledHitBuffer[index]);
            }
        }

        private void AddScheduledHit(ScheduledProjectileHit hit)
        {
            if (!scheduledHitsByTick.TryGetValue(
                hit.HitTick,
                out List<ScheduledProjectileHit> hits))
            {
                hits = new List<ScheduledProjectileHit>();
                scheduledHitsByTick.Add(hit.HitTick, hits);
            }

            hits.Add(hit);
        }

        private bool ShouldPresentImpact(ScheduledProjectileHit hit)
        {
            if (hit.ImpactGroupId == 0 || !presentedImpactGroupIds.Add(hit.ImpactGroupId))
            {
                return false;
            }

            lastPresentedImpacts[hit.ProjectileId] =
                new ProjectileImpactHistory(hit.HitTick, hit.Position);
            return true;
        }

        private HashSet<long> GetHitEnemyIds(long projectileId)
        {
            if (!hitEnemyIdsByProjectile.TryGetValue(
                projectileId,
                out HashSet<long> hitEnemyIds))
            {
                hitEnemyIds = new HashSet<long>();
                hitEnemyIdsByProjectile.Add(projectileId, hitEnemyIds);
            }

            return hitEnemyIds;
        }

    }
}
