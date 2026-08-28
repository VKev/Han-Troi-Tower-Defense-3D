using System.Collections.Generic;
using NUnit.Framework;
using TowerDefense3D.Towers;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.Enemies.Tests.EditMode
{
    public sealed class ElementReactionAssetTests
    {
        private const string CatalogPath = "Assets/Config/Combat/ElementReactionCatalog.asset";
        private const string CombatRulesPath = "Assets/Config/Towers/Global/TowerCombatRules.asset";

        [Test]
        public void ApprovedReactionCatalog_PassesItsOwnValidation()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ElementReactionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, $"Element Reaction Catalog is missing at '{CatalogPath}'.");

            IReadOnlyList<string> errors = catalog.CollectValidationErrors();

            Assert.That(errors, Is.Empty, string.Join("\n", errors));
        }

        [Test]
        public void EveryLiftReaction_GrantsImmunitySoAnEnemyCannotBeHeldAirborneForever()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ElementReactionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);

            for (int index = 0; index < catalog.Definitions.Count; index++)
            {
                ElementReactionDefinition reaction = catalog.Definitions[index];
                if (reaction == null || reaction.LiftDurationSeconds <= 0f)
                {
                    continue;
                }

                // A lift deals no damage and stops the enemy moving, so without a positive
                // immunity window a chain that re-triggers faster than the lift lasts holds
                // the enemy in place forever. Combat planning then never converges and
                // TryStartWave fails, which is what made Start Wave look dead.
                Assert.That(
                    reaction.LiftImmunitySeconds,
                    Is.GreaterThan(0f),
                    $"{reaction.name} lifts for {reaction.LiftDurationSeconds}s with no immunity window.");
            }
        }

        [Test]
        public void AuthoredLiftAndPushCeilings_LeaveEveryEnemyNetForwardProgress()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ElementReactionCatalog>(CatalogPath);
            var combatRules = AssetDatabase.LoadAssetAtPath<TowerCombatRules>(CombatRulesPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(combatRules, Is.Not.Null, $"Tower Combat Rules are missing at '{CombatRulesPath}'.");

            float longestLiftUptime = 0f;
            for (int index = 0; index < catalog.Definitions.Count; index++)
            {
                ElementReactionDefinition reaction = catalog.Definitions[index];
                if (reaction == null || reaction.LiftDurationSeconds <= 0f)
                {
                    continue;
                }

                float cycleSeconds = reaction.LiftDurationSeconds + reaction.LiftImmunitySeconds;
                longestLiftUptime = Mathf.Max(
                    longestLiftUptime,
                    reaction.LiftDurationSeconds / cycleSeconds);
            }

            float progressFraction =
                (1f - longestLiftUptime) * (1f - combatRules.MaximumPushSpeedFraction);

            // Combat planning simulates a whole wave up front and only stops once every enemy
            // has died or leaked, so the authored ceilings must leave real forward progress.
            Assert.That(
                progressFraction,
                Is.GreaterThan(0f),
                $"Lift uptime {longestLiftUptime:P0} and push ceiling "
                + $"{combatRules.MaximumPushSpeedFraction:P0} would stall an enemy forever.");
        }
    }
}
