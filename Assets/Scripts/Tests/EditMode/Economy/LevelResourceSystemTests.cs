using NUnit.Framework;
using TowerDefense3D.Economy;

namespace TowerDefense3D.GameFlow.Tests.EditMode
{
    public sealed class LevelResourceSystemTests
    {
        [Test]
        public void GoldSystem_SpendsOnlyWhenAffordableAndResetsToItsAuthoredValue()
        {
            var goldSystem = new LevelGoldSystem(100);

            Assert.That(goldSystem.TrySpend(70), Is.True);
            Assert.That(goldSystem.Balance, Is.EqualTo(30));
            Assert.That(goldSystem.TrySpend(31), Is.False);
            Assert.That(goldSystem.Balance, Is.EqualTo(30));

            goldSystem.Add(15);
            goldSystem.Reset();

            Assert.That(goldSystem.Balance, Is.EqualTo(100));
        }

        [Test]
        public void BaseHealthSystem_ClampsLeakDamageAtZeroAndResetsToItsAuthoredValue()
        {
            var healthSystem = new LevelBaseHealthSystem(10);

            healthSystem.TakeDamage(3);
            healthSystem.TakeDamage(20);

            Assert.That(healthSystem.CurrentHealth, Is.Zero);
            Assert.That(healthSystem.IsDepleted, Is.True);

            healthSystem.Reset();

            Assert.That(healthSystem.CurrentHealth, Is.EqualTo(10));
            Assert.That(healthSystem.IsDepleted, Is.False);
        }
    }
}
