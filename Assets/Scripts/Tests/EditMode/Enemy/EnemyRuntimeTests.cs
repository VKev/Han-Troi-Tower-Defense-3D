using NUnit.Framework;
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
    }
}
