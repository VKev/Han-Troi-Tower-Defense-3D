using System.Collections.Generic;
using NUnit.Framework;
using TowerDefense3D.Enemies;
using TowerDefense3D.Towers;
using UnityEditor;

namespace TowerDefense3D.Tests.EditMode
{
    public sealed class ElementReactionCatalogTests
    {
        private const string CatalogPath =
            "Assets/Config/Combat/ElementReactionCatalog.asset";

        [Test]
        public void AuthoredCatalog_DefinesEveryElementPair()
        {
            ElementReactionCatalog catalog =
                AssetDatabase.LoadAssetAtPath<ElementReactionCatalog>(CatalogPath);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.ElementMarkDurationSeconds, Is.EqualTo(3f));
            Assert.That(catalog.ReactionCooldownSeconds, Is.EqualTo(0.5f));
            Assert.That(catalog.MaximumSlowFraction, Is.EqualTo(0.7f));
            Assert.That(catalog.Definitions, Has.Count.EqualTo(10));
            Assert.That(catalog.CollectValidationErrors(), Is.Empty);

            var pairs = new HashSet<ElementPair>();
            for (int index = 0; index < catalog.Definitions.Count; index++)
            {
                Assert.That(pairs.Add(catalog.Definitions[index].Pair), Is.True);
            }
        }

        [Test]
        public void PureRewritePairs_AreTerminalAndHaveNoSpecialEffect()
        {
            ElementReactionCatalog catalog =
                AssetDatabase.LoadAssetAtPath<ElementReactionCatalog>(CatalogPath);

            AssertPureRewrite(catalog.Get(ElementType.Fire, ElementType.Earth));
            AssertPureRewrite(catalog.Get(ElementType.Water, ElementType.Wind));
        }

        private static void AssertPureRewrite(ElementReactionDefinition definition)
        {
            Assert.That(definition.ReactionId, Is.EqualTo(ElementReactionId.PureRewrite));
            Assert.That(definition.PhysicalDamage, Is.Zero);
            Assert.That(definition.MagicDamage, Is.Zero);
            Assert.That(definition.RadiusMeters, Is.Zero);
            Assert.That(definition.BurnDamagePerTick, Is.Zero);
            Assert.That(definition.SlowStrengthFraction, Is.Zero);
            Assert.That(definition.PushDistanceMeters, Is.Zero);
            Assert.That(definition.PhysicalResistanceReductionPoints, Is.Zero);
            Assert.That(definition.MagicResistanceReductionPoints, Is.Zero);
            Assert.That(definition.CreatesField, Is.False);
        }
    }
}
