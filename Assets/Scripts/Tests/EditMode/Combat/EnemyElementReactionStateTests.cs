using NUnit.Framework;
using TowerDefense3D.Enemies;
using TowerDefense3D.Towers;
using UnityEditor;

namespace TowerDefense3D.Tests.EditMode
{
    public sealed class EnemyElementReactionStateTests
    {
        private const string CatalogPath =
            "Assets/Config/Combat/ElementReactionCatalog.asset";
        private const float TickSeconds = 0.05f;

        private ElementReactionCatalog catalog;

        [SetUp]
        public void SetUp()
        {
            catalog = AssetDatabase.LoadAssetAtPath<ElementReactionCatalog>(CatalogPath);
        }

        [Test]
        public void FirstElement_MarksEnemyForAuthoredDuration()
        {
            var state = new EnemyElementReactionState(catalog, TickSeconds);

            bool reacted = state.TryReceive(
                ElementType.Fire,
                1f,
                10L,
                out ElementReactionDefinition reaction);

            Assert.That(reacted, Is.False);
            Assert.That(reaction, Is.Null);
            Assert.That(state.Phase, Is.EqualTo(EnemyElementPhase.Marked));
            Assert.That(state.Element, Is.EqualTo(ElementType.Fire));
            Assert.That(state.GetRemainingSeconds(10L), Is.EqualTo(3f));
        }

        [Test]
        public void SecondElement_ConsumesEnemyTokenAndStartsCooldown()
        {
            var state = new EnemyElementReactionState(catalog, TickSeconds);
            state.TryReceive(ElementType.Fire, 1f, 10L, out _);

            bool reacted = state.TryReceive(
                ElementType.Water,
                1f,
                11L,
                out ElementReactionDefinition reaction);

            Assert.That(reacted, Is.True);
            Assert.That(reaction.ReactionId, Is.EqualTo(ElementReactionId.ThermalShock));
            Assert.That(state.Phase, Is.EqualTo(EnemyElementPhase.ReactionCooldown));
            Assert.That(state.GetRemainingSeconds(11L), Is.EqualTo(0.5f));
        }

        [Test]
        public void Cooldown_DiscardsElementWithoutExtendingCooldown()
        {
            var state = new EnemyElementReactionState(catalog, TickSeconds);
            state.TryReceive(ElementType.Fire, 1f, 10L, out _);
            state.TryReceive(ElementType.Water, 1f, 11L, out _);

            bool reacted = state.TryReceive(
                ElementType.Earth,
                1f,
                13L,
                out ElementReactionDefinition reaction);

            Assert.That(reacted, Is.False);
            Assert.That(reaction, Is.Null);
            Assert.That(state.Phase, Is.EqualTo(EnemyElementPhase.ReactionCooldown));
            Assert.That(state.GetRemainingSeconds(13L), Is.EqualTo(0.4f).Within(0.0001f));

            state.Advance(21L);

            Assert.That(state.Phase, Is.EqualTo(EnemyElementPhase.Ready));
        }

        [Test]
        public void ReactionToken_IsOwnedIndependentlyByEachEnemy()
        {
            var firstEnemy = new EnemyElementReactionState(catalog, TickSeconds);
            var secondEnemy = new EnemyElementReactionState(catalog, TickSeconds);
            firstEnemy.TryReceive(ElementType.Fire, 1f, 10L, out _);
            firstEnemy.TryReceive(ElementType.Water, 1f, 11L, out _);
            secondEnemy.TryReceive(ElementType.Fire, 1f, 10L, out _);

            bool firstReacted = firstEnemy.TryReceive(
                ElementType.Wind,
                1f,
                12L,
                out _);
            bool secondReacted = secondEnemy.TryReceive(
                ElementType.Wind,
                1f,
                12L,
                out ElementReactionDefinition secondReaction);

            Assert.That(firstReacted, Is.False);
            Assert.That(secondReacted, Is.True);
            Assert.That(secondReaction.ReactionId, Is.EqualTo(ElementReactionId.Firestorm));
        }
    }
}
