using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.Enemies.Tests.EditMode
{
    public sealed class EnemyRuntimeTests
    {
        private EnemyDefinition definition;

        [SetUp]
        public void SetUp()
        {
            definition = ScriptableObject.CreateInstance<EnemyDefinition>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(definition);
        }

        [Test]
        public void Step_MovesEnemyAlongRoadAndLeaksAtEnd()
        {
            var road = new RoadPath(new[]
            {
                Vector3.zero,
                Vector3.right
            });
            var system = new EnemySystem(road);
            int leakCount = 0;
            system.EnemyLeaked += _ => leakCount++;

            EnemyInstance enemy = system.Spawn(definition);
            system.Step(0.25f);

            Assert.That(enemy.Position.x, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(system.LivingCount, Is.EqualTo(1));

            system.Step(0.25f);

            Assert.That(system.LivingCount, Is.Zero);
            Assert.That(leakCount, Is.EqualTo(1));
        }

        [Test]
        public void ApplyDamage_KillsEnemyOnlyWhenHealthReachesZero()
        {
            var system = new EnemySystem(new RoadPath(new[]
            {
                Vector3.zero,
                Vector3.right * 10f
            }));
            EnemyInstance enemy = system.Spawn(definition);

            Assert.That(system.ApplyDamage(enemy.Id, definition.BaseMaxHealth - 1f), Is.False);
            Assert.That(system.ApplyDamage(enemy.Id, 1f), Is.True);
            Assert.That(system.LivingCount, Is.Zero);
        }

        [Test]
        public void SpeedSupport_UsesStrongestNearbyAuraWithoutBuffingItself()
        {
            SpeedSupportEnemyDefinition support = AssetDatabase.LoadAssetAtPath<SpeedSupportEnemyDefinition>(
                "Assets/Config/Enemies/SpeedSupport.asset");
            EnemyDefinition basic = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(
                "Assets/Config/Enemies/Basic.asset");
            var system = CreateLongRoadSystem();

            EnemyInstance supportEnemy = system.Spawn(support);
            EnemyInstance basicEnemy = system.Spawn(basic);
            system.Step(0.5f);

            float expectedBasicDistance = basic.BaseMoveSpeed
                * (1f + support.RegularSpeedBonusFraction)
                * 0.5f;
            Assert.That(basicEnemy.Position.x, Is.EqualTo(expectedBasicDistance).Within(0.0001f));
            Assert.That(
                supportEnemy.Position.x,
                Is.EqualTo(support.BaseMoveSpeed * 0.5f).Within(0.0001f));
        }

        [Test]
        public void StealthEnemy_DirectHitRevealsTemporarily()
        {
            StealthEnemyDefinition stealth = AssetDatabase.LoadAssetAtPath<StealthEnemyDefinition>(
                "Assets/Config/Enemies/Stealth.asset");
            var system = CreateLongRoadSystem();
            EnemyInstance enemy = system.Spawn(stealth);

            Assert.That(enemy.IsHidden, Is.True);

            system.RevealFromDirectHit(enemy.Id);
            Assert.That(enemy.IsHidden, Is.False);

            system.Step(stealth.RevealDurationSeconds - 0.01f);
            Assert.That(enemy.IsHidden, Is.False);

            system.Step(0.02f);
            Assert.That(enemy.IsHidden, Is.True);
        }

        [Test]
        public void SummonerBoss_SpawnsAuthoredPhaseEntriesAtItsRoadProgress()
        {
            SummonerBossEnemyDefinition bossDefinition =
                AssetDatabase.LoadAssetAtPath<SummonerBossEnemyDefinition>(
                    "Assets/Config/Enemies/SummonerBoss.asset");
            var system = CreateLongRoadSystem();
            var snapshots = new List<EnemySnapshot>();

            system.Spawn(bossDefinition);
            system.Step(bossDefinition.SummonPhases[0].SummonIntervalSeconds);
            system.CopySnapshotsTo(snapshots);

            int summonedCount = 0;
            for (int index = 0; index < snapshots.Count; index++)
            {
                if (snapshots[index].IsSummoned)
                {
                    summonedCount++;
                }
            }

            Assert.That(system.LivingCount, Is.EqualTo(3));
            Assert.That(summonedCount, Is.EqualTo(2));
        }

        private static EnemySystem CreateLongRoadSystem()
        {
            return new EnemySystem(new RoadPath(new[]
            {
                Vector3.zero,
                Vector3.right * 1000f
            }));
        }
    }
}
