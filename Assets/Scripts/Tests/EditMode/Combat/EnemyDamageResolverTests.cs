using NUnit.Framework;
using TowerDefense3D.Towers;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.Enemies.Tests.EditMode
{
    public sealed class EnemyDamageResolverTests
    {
        private EnemyDefinition enemy;

        [SetUp]
        public void SetUp()
        {
            enemy = ScriptableObject.CreateInstance<EnemyDefinition>();
            var serialized = new SerializedObject(enemy);
            serialized.FindProperty("basePhysicalResistance").floatValue = 25f;
            serialized.FindProperty("baseMagicResistance").floatValue = 50f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(enemy);
        }

        [TestCase(DamageType.Physical, 75f)]
        [TestCase(DamageType.Magic, 50f)]
        [TestCase(DamageType.True, 100f)]
        public void Resolve_AppliesTheSelectedResistance(
            DamageType damageType,
            float expectedDamage)
        {
            Assert.That(
                EnemyDamageResolver.Resolve(100f, damageType, enemy),
                Is.EqualTo(expectedDamage).Within(0.0001f));
        }

        [Test]
        public void Resolve_ClampsEffectiveResistanceToApprovedRange()
        {
            float vulnerableDamage = EnemyDamageResolver.Resolve(
                100f,
                DamageType.Physical,
                enemy,
                -125f);
            float cappedDefenseDamage = EnemyDamageResolver.Resolve(
                100f,
                DamageType.Physical,
                enemy,
                100f);

            Assert.That(vulnerableDamage, Is.EqualTo(200f).Within(0.0001f));
            Assert.That(cappedDefenseDamage, Is.EqualTo(10f).Within(0.0001f));
        }

        [Test]
        public void Resolve_ResolvesPhysicalAndMagicFromOneDefenseSnapshot()
        {
            ResolvedDamage result = EnemyDamageResolver.Resolve(
                new DamageChannels(100f, 100f, 5f),
                enemy);

            Assert.That(result.Physical, Is.EqualTo(75f).Within(0.0001f));
            Assert.That(result.Magic, Is.EqualTo(50f).Within(0.0001f));
            Assert.That(result.True, Is.EqualTo(5f));
            Assert.That(result.Total, Is.EqualTo(130f).Within(0.0001f));
        }
    }
}
