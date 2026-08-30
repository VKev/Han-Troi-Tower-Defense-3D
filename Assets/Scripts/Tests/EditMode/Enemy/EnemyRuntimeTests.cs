using System.Collections.Generic;
using NUnit.Framework;
using TowerDefense3D.Economy;
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
            var healthSystem = new LevelBaseHealthSystem(10);
            var system = new EnemySystem(road, new LevelGoldSystem(0), healthSystem);
            int leakCount = 0;
            system.EnemyLeaked += _ => leakCount++;

            EnemyInstance enemy = system.Spawn(definition);
            system.Step(EnemySpawnPresentationTiming.SpawnMovementDelaySeconds);
            system.Step(0.25f);

            Assert.That(enemy.Position.x, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(system.LivingCount, Is.EqualTo(1));

            system.Step(0.25f);

            Assert.That(system.LivingCount, Is.Zero);
            Assert.That(leakCount, Is.EqualTo(1));
            Assert.That(healthSystem.CurrentHealth, Is.EqualTo(9));
        }

        [Test]
        public void Step_HoldsEnemyAtRoadSpawnUntilPresentationCompletes()
        {
            var system = CreateLongRoadSystem();
            EnemyInstance enemy = system.Spawn(definition);

            system.Step(EnemySpawnPresentationTiming.SpawnMovementDelaySeconds - 0.01f);
            Assert.That(enemy.Position, Is.EqualTo(Vector3.zero));

            system.Step(0.02f);
            Assert.That(
                enemy.Position.x,
                Is.EqualTo(definition.BaseMoveSpeed * 0.01f).Within(0.0001f));
        }

        [Test]
        public void ApplyDamage_KillsEnemyOnlyWhenHealthReachesZero()
        {
            var goldSystem = new LevelGoldSystem(0);
            var system = new EnemySystem(new RoadPath(new[]
            {
                Vector3.zero,
                Vector3.right * 10f
            }), goldSystem, new LevelBaseHealthSystem(10));
            EnemyInstance enemy = system.Spawn(definition);

            Assert.That(system.ApplyDamage(enemy.Id, definition.BaseMaxHealth - 1f), Is.False);
            Assert.That(system.ApplyDamage(enemy.Id, 1f), Is.True);
            Assert.That(system.LivingCount, Is.Zero);
            Assert.That(goldSystem.Balance, Is.EqualTo(definition.GoldOnDeath));
        }

        [Test]
        public void SpeedSupport_AppliesNearbyBuffWhenSkillCastStarts()
        {
            SpeedSupportEnemyDefinition support = AssetDatabase.LoadAssetAtPath<SpeedSupportEnemyDefinition>(
                "Assets/Config/Enemies/SpeedSupport.asset");
            EnemyDefinition basic = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(
                "Assets/Config/Enemies/Basic.asset");
            var system = CreateLongRoadSystem();

            EnemyInstance supportEnemy = system.Spawn(support);
            EnemyInstance basicEnemy = system.Spawn(basic);
            SkipSpawnDelay(supportEnemy, basicEnemy);
            system.Step(support.ActivationDelaySeconds);

            Assert.That(supportEnemy.IsSpeedAuraActive, Is.True);
            Assert.That(supportEnemy.SkillCastVersion, Is.EqualTo(1));
            Assert.That(basicEnemy.IsSpeedBuffed, Is.True);
            Assert.That(
                basicEnemy.Position.x,
                Is.EqualTo(basic.BaseMoveSpeed * (1f + support.RegularSpeedBonusFraction)
                    * support.ActivationDelaySeconds).Within(0.0001f));
            Assert.That(supportEnemy.Position.x, Is.Zero);

            // The buffed enemy runs exactly auraRadiusMeters ahead by the time the aura turns
            // on, so it leaves the aura on the very next step and falls back to its base speed.
            float positionBeforeBuff = basicEnemy.Position.x;
            system.Step(0.5f);

            float expectedBasicDistance = basic.BaseMoveSpeed * 0.5f;
            Assert.That(basicEnemy.Position.x - positionBeforeBuff, Is.EqualTo(expectedBasicDistance).Within(0.0001f));
            Assert.That(basicEnemy.IsSpeedBuffed, Is.False);
            Assert.That(
                supportEnemy.Position.x,
                Is.Zero);
        }

        [Test]
        public void SpeedSupport_StopsMovingDuringSkillCast()
        {
            SpeedSupportEnemyDefinition support = AssetDatabase.LoadAssetAtPath<SpeedSupportEnemyDefinition>(
                "Assets/Config/Enemies/SpeedSupport.asset");
            var system = CreateLongRoadSystem();
            EnemyInstance enemy = system.Spawn(support);
            SkipSpawnDelay(enemy);

            system.Step(support.ActivationDelaySeconds);
            float positionAtCastStart = enemy.Position.x;
            system.Step(support.SkillDurationSeconds);

            Assert.That(enemy.Position.x, Is.EqualTo(positionAtCastStart).Within(0.0001f));
            Assert.That(enemy.IsSpeedAuraActive, Is.True);

            system.Step(0.05f);
            Assert.That(enemy.Position.x, Is.GreaterThan(positionAtCastStart));
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
            Assert.That(system.LivingCount, Is.EqualTo(1));

            system.Step(bossDefinition.SummonSkillDurationSeconds);
            system.CopySnapshotsTo(snapshots);

            int summonedCount = 0;
            for (int index = 0; index < snapshots.Count; index++)
            {
                if (snapshots[index].IsSummoned)
                {
                    summonedCount++;
                }
            }

            // Derived from the asset rather than hard-coded, so retuning how many enemies a
            // phase summons stays a balance change instead of a failing test.
            int authored = 0;
            for (int index = 0; index < bossDefinition.SummonPhases[0].Entries.Count; index++)
            {
                authored += bossDefinition.SummonPhases[0].Entries[index].Count;
            }

            Assert.That(summonedCount, Is.EqualTo(authored));
            Assert.That(system.LivingCount, Is.EqualTo(authored + 1));
        }

        private static EnemySystem CreateLongRoadSystem()
        {
            return new EnemySystem(new RoadPath(new[]
            {
                Vector3.zero,
                Vector3.right * 1000f
            }), new LevelGoldSystem(0), new LevelBaseHealthSystem(10));
        }

        private static void SkipSpawnDelay(params EnemyInstance[] enemies)
        {
            for (int index = 0; index < enemies.Length; index++)
            {
                enemies[index].SpawnDelayRemainingSeconds = 0f;
            }
        }
    }
}
