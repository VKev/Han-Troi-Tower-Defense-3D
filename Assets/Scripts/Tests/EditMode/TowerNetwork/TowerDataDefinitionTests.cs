using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TowerDefense3D.GridPlacement;
using UnityEngine;

namespace TowerDefense3D.Towers.Tests.EditMode
{
    public sealed class TowerDataDefinitionTests
    {
        private readonly List<UnityEngine.Object> instances = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object instance in instances)
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            instances.Clear();
        }

        [Test]
        public void DefaultDefinitions_AreValidBeforePlacementReferencesAreAssigned()
        {
            TowerCombatDefinition[] definitions =
            {
                Create<GeneratorTowerDefinition>(),
                Create<FireTowerDefinition>(),
                Create<WaterTowerDefinition>(),
                Create<WindTowerDefinition>(),
                Create<EarthTowerDefinition>(),
                Create<SoulNexusDefinition>()
            };

            foreach (TowerCombatDefinition definition in definitions)
            {
                Assert.That(
                    TowerDataValidator.CollectErrors(definition, false),
                    Is.Empty,
                    definition.Family.ToString());
            }
        }

        [Test]
        public void ProducingDefinition_RequiresProjectileAndHitEffectPrefabs()
        {
            GeneratorTowerDefinition generator = Create<GeneratorTowerDefinition>();
            TowerDefinition placementDefinition = Create<TowerDefinition>();
            SetField(generator.Core, "placementDefinition", placementDefinition);

            string combined = string.Join(
                " | ",
                TowerDataValidator.CollectErrors(generator));

            StringAssert.Contains("Projectile Prefab is required", combined);
            StringAssert.Contains("Hit Effect Prefab is required", combined);
        }

        [Test]
        public void CombatRules_AllowGeneratorDirectlyToSoulNexus()
        {
            TowerCombatRules rules = Create<TowerCombatRules>();

            Assert.That(rules.MinimumProcessorCountInValidChain, Is.Zero);
            Assert.That(rules.MinimumElementCountInValidChain, Is.Zero);
            Assert.That(TowerDataValidator.CollectErrors(rules), Is.Empty);
            Assert.That(
                rules.DefenseResolutionOrder,
                Is.EqualTo(new[]
                {
                    DefenseResolutionStep.StrongestEarthReduction,
                    DefenseResolutionStep.PercentPenetration,
                    DefenseResolutionStep.FlatPenetration,
                    DefenseResolutionStep.ClampToMinimum,
                    DefenseResolutionStep.Mitigation,
                    DefenseResolutionStep.DamageTakenModifier
                }));
        }

        [Test]
        public void FireTierOne_EmitsThreeIndependentFireDamageConservingProjectiles()
        {
            FireTowerDefinition fire = Create<FireTowerDefinition>();

            Assert.That(fire.TierOne.OutputProjectileCount, Is.EqualTo(3));
            Assert.That(fire.TierOne.RequiredDownstreamReservationCount, Is.EqualTo(3));
            Assert.That(fire.TierOne.SequenceSpacingSeconds, Is.EqualTo(0.08f));
            Assert.That(fire.TierOne.ConservesTotalFireDamageAcrossClones,Is.True);
            Assert.That(fire.TierOne.ProjectilesHaveIndependentIdsAndHitSets, Is.True);
        }

        [Test]
        public void ApprovedWaterBranches_UseThirtyFiveAndFiftyFivePercentTotalSpeed()
        {
            WaterTowerDefinition water = Create<WaterTowerDefinition>();

            float stackBranchTotal = water.TierOne.ProcessSpeedBonusFraction +
                                     water.WaterStackBranch.EvolutionProcessSpeedBonusFraction;
            float pressureBranchTotal = water.TierOne.ProcessSpeedBonusFraction +
                                        water.PressureBranch.TierTwoProcessSpeedBonusFraction;

            Assert.That(stackBranchTotal, Is.EqualTo(0.35f).Within(0.0001f));
            Assert.That(pressureBranchTotal, Is.EqualTo(0.55f).Within(0.0001f));
        }

        [Test]
        public void Catalog_RejectsDuplicateFamilyAndStableId()
        {
            TowerCombatRules rules = Create<TowerCombatRules>();
            FireTowerDefinition first = Create<FireTowerDefinition>();
            FireTowerDefinition second = Create<FireTowerDefinition>();
            TowerCatalog catalog = Create<TowerCatalog>();
            SetField(catalog, "combatRules", rules);
            SetField(
                catalog,
                "definitions",
                new List<TowerCombatDefinition> { first, second });

            string combined = string.Join(
                " | ",
                TowerDataValidator.CollectErrors(catalog, false));

            StringAssert.Contains("Duplicate Tower Stable Id", combined);
            StringAssert.Contains("Duplicate Tower Family", combined);
        }

        [Test]
        public void MalformedDefinition_ReturnsErrorsInsteadOfThrowing()
        {
            GeneratorTowerDefinition generator = Create<GeneratorTowerDefinition>();
            SetField<TowerCoreProfile>(generator, "core", null);

            IReadOnlyList<string> errors = null;
            Assert.DoesNotThrow(
                () => errors = TowerDataValidator.CollectErrors(generator, false));
            Assert.That(errors, Is.Not.Empty);
        }

        [Test]
        public void MalformedWaterSlow_ReturnsErrorsInsteadOfThrowing()
        {
            WaterTowerDefinition water = Create<WaterTowerDefinition>();
            SetField<SlowProfile>(water, "slow", null);

            IReadOnlyList<string> errors = null;
            Assert.DoesNotThrow(
                () => errors = TowerDataValidator.CollectErrors(water, false));
            Assert.That(errors, Is.Not.Empty);
        }

        [Test]
        public void MalformedCatalog_ReturnsErrorsInsteadOfThrowing()
        {
            TowerCatalog catalog = Create<TowerCatalog>();
            SetField(catalog, "combatRules", Create<TowerCombatRules>());
            SetField<List<TowerCombatDefinition>>(catalog, "definitions", null);

            IReadOnlyList<string> errors = null;
            Assert.DoesNotThrow(
                () => errors = TowerDataValidator.CollectErrors(catalog, false));
            StringAssert.Contains("definition list", string.Join(" | ", errors));
        }

        [Test]
        public void CombatRules_RejectInvalidProgressionLimit()
        {
            TowerCombatRules rules = Create<TowerCombatRules>();
            SetField(rules, "maximumTierThreeElementTowers", 3);

            Assert.That(TowerDataValidator.CollectErrors(rules), Is.Not.Empty);
        }

        private T Create<T>() where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            instances.Add(instance);
            return instance;
        }

        private static void SetField<T>(object target, string name, T value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Expected serialized field '{name}'.");
            field.SetValue(target, value);
        }
    }
}
