using System.Collections.Generic;
using NUnit.Framework;
using TowerDefense3D.Towers;
using TowerDefense3D.Waves;
using UnityEngine;

namespace TowerDefense3D.Enemies.Tests.EditMode
{
    public sealed class ProjectileHitPlannerTests
    {
        private EnemyDefinition enemy;

        [SetUp]
        public void SetUp()
        {
            enemy = ScriptableObject.CreateInstance<EnemyDefinition>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(enemy);
        }

        [Test]
        public void TryCreateScheduledHit_PrecomputesCrossingTick()
        {
            ProjectileHitPlanner planner = CreatePlanner();
            WaveSpawnOrder spawn = new WaveSpawnOrder(0f, enemy, 0).WithEnemyId(1L);
            EnemyTrajectoryPlan enemyTrajectory = planner.CreateWaveEnemyTrajectories(
                new[] { spawn })[0];
            ProjectileTrajectoryPlan projectileTrajectory = planner.CreateProjectileTrajectory(
                CreateProjectile(),
                new TowerWorldPosition(1f, 0f, 0f),
                0L);

            bool scheduled = planner.TryCreateScheduledHit(
                projectileTrajectory,
                enemyTrajectory,
                out ScheduledProjectileHit hit);

            Assert.That(scheduled, Is.True);
            Assert.That(hit.ProjectileId, Is.EqualTo(1L));
            Assert.That(hit.EnemyId, Is.EqualTo(1L));
            Assert.That(hit.HitTick, Is.InRange(1L, 20L));
            Assert.That(hit.Position.x, Is.InRange(-0.5f, 0.5f));
        }

        [Test]
        public void TryCreateScheduledHit_RejectsEnemySpawningAfterProjectileArrival()
        {
            ProjectileHitPlanner planner = CreatePlanner();
            WaveSpawnOrder spawn = new WaveSpawnOrder(2f, enemy, 0).WithEnemyId(1L);
            EnemyTrajectoryPlan enemyTrajectory = planner.CreateWaveEnemyTrajectories(
                new[] { spawn })[0];
            ProjectileTrajectoryPlan projectileTrajectory = planner.CreateProjectileTrajectory(
                CreateProjectile(),
                new TowerWorldPosition(1f, 0f, 0f),
                0L);

            bool scheduled = planner.TryCreateScheduledHit(
                projectileTrajectory,
                enemyTrajectory,
                out _);

            Assert.That(scheduled, Is.False);
        }

        [Test]
        public void CreateWaveEnemyTrajectories_AppliesSpeedSupportAura()
        {
            var support = ScriptableObject.CreateInstance<SpeedSupportEnemyDefinition>();
            try
            {
                ProjectileHitPlanner planner = CreatePlanner();
                IReadOnlyList<EnemyTrajectoryPlan> trajectories =
                    planner.CreateWaveEnemyTrajectories(new[]
                    {
                        new WaveSpawnOrder(0f, enemy, 0).WithEnemyId(1L),
                        new WaveSpawnOrder(0f, support, 1).WithEnemyId(2L)
                    });

                TimedTrajectorySegment normalLastSegment =
                    trajectories[0].Segments[trajectories[0].Segments.Count - 1];
                TimedTrajectorySegment supportLastSegment =
                    trajectories[1].Segments[trajectories[1].Segments.Count - 1];

                Assert.That(
                    normalLastSegment.EndTimeSeconds,
                    Is.LessThan(supportLastSegment.EndTimeSeconds));
            }
            finally
            {
                Object.DestroyImmediate(support);
            }
        }

        private static ProjectileHitPlanner CreatePlanner()
        {
            var road = new RoadPath(new[]
            {
                new Vector3(0f, 0f, -1f),
                new Vector3(0f, 0f, 1f)
            });
            return new ProjectileHitPlanner(
                road,
                0.05f,
                2f,
                0.2f);
        }

        private static TowerProjectileSnapshot CreateProjectile()
        {
            return new TowerProjectileSnapshot(
                1L,
                new TowerNodeId(1),
                new TowerNodeId(2),
                new TowerWorldPosition(-1f, 0f, 0f),
                new ProjectilePayload(
                    ProjectilePayloadKind.Physical,
                    1f,
                    DamageType.Physical),
                0);
        }
    }
}
