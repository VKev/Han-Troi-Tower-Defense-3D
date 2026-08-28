using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.Enemies.Tests.EditMode
{
    public sealed class EnemyThermalShieldViewTests
    {
        private const string MagicResistantPrefabPath =
            "Assets/Resources/Prefabs/Enemies/MagicResistant 1.prefab";
        private const string MagicResistantDefinitionPath =
            "Assets/Config/Enemies/MagicResistant.asset";
        private const string BasicDefinitionPath = "Assets/Config/Enemies/Basic.asset";

        [Test]
        public void Prefab_AuthorsAShieldViewBoundToTheShieldMesh()
        {
            GameObject owner = PrefabUtility.LoadPrefabContents(MagicResistantPrefabPath);
            try
            {
                var view = owner.GetComponentInChildren<EnemyThermalShieldView>(true);
                Assert.That(view, Is.Not.Null, "Magic Resistant must author a thermal shield view.");

                var serialized = new SerializedObject(view);
                var shieldRoot = serialized.FindProperty("shieldRoot").objectReferenceValue as GameObject;

                Assert.That(shieldRoot, Is.Not.Null, "The shield view needs its shield mesh.");
                Assert.That(
                    shieldRoot.GetComponentsInChildren<Renderer>(true).Length,
                    Is.GreaterThan(0),
                    "The shield mesh must have something to fade.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(owner);
            }
        }

        [Test]
        public void ShieldFadesByRemainingHitsAndSwitchesOffWhenBroken()
        {
            var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(MagicResistantDefinitionPath);
            Assert.That(definition, Is.Not.Null);
            Assert.That(
                definition.ThermalShockHitsToBreakShield,
                Is.EqualTo(2),
                "The fade steps assume the authored two-hit shield.");

            GameObject owner = PrefabUtility.LoadPrefabContents(MagicResistantPrefabPath);
            try
            {
                var view = owner.GetComponentInChildren<EnemyThermalShieldView>(true);
                var serialized = new SerializedObject(view);
                var shieldRoot = serialized.FindProperty("shieldRoot").objectReferenceValue as GameObject;

                view.Bind(CreateSnapshot(definition, remainingHits: 2));
                Assert.That(shieldRoot.activeSelf, Is.True, "A full shield must be visible.");

                view.Bind(CreateSnapshot(definition, remainingHits: 1));
                Assert.That(shieldRoot.activeSelf, Is.True, "A half shield must still be visible.");

                view.Bind(CreateSnapshot(definition, remainingHits: 0));
                Assert.That(shieldRoot.activeSelf, Is.False, "A broken shield must switch off.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(owner);
            }
        }

        [Test]
        public void EnemiesWithoutAnAuthoredShield_KeepTheShieldHidden()
        {
            var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(BasicDefinitionPath);
            Assert.That(definition, Is.Not.Null);
            Assert.That(
                definition.ThermalShockHitsToBreakShield,
                Is.Zero,
                "Only shielded enemies should carry thermal shield hits.");

            GameObject owner = PrefabUtility.LoadPrefabContents(MagicResistantPrefabPath);
            try
            {
                var view = owner.GetComponentInChildren<EnemyThermalShieldView>(true);
                var serialized = new SerializedObject(view);
                var shieldRoot = serialized.FindProperty("shieldRoot").objectReferenceValue as GameObject;

                view.Bind(CreateSnapshot(definition, remainingHits: 0));

                Assert.That(shieldRoot.activeSelf, Is.False);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(owner);
            }
        }

        private static EnemySnapshot CreateSnapshot(EnemyDefinition definition, int remainingHits)
        {
            return new EnemySnapshot(
                1L,
                definition,
                Vector3.zero,
                Vector3.zero,
                definition.BaseMaxHealth,
                false,
                false,
                default,
                remainingHits);
        }
    }
}
