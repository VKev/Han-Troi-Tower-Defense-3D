using System.Collections.Generic;
using NUnit.Framework;
using TowerDefense3D.Enemies;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.Enemies.Tests.EditMode
{
    public sealed class EnemyDataDefinitionTests
    {
        private const string CatalogPath = "Assets/Config/Enemies/EnemyCatalog.asset";

        [Test]
        public void ApprovedEnemyCatalog_ContainsEightValidDefinitions()
        {
            EnemyCatalog catalog = AssetDatabase.LoadAssetAtPath<EnemyCatalog>(CatalogPath);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.CollectValidationErrors(), Is.Empty);
            Assert.That(catalog.Definitions, Has.Count.EqualTo(8));

            EnemyDefinition basic = Get(catalog, "basic");
            EnemyDefinition armored = Get(catalog, "armored");
            EnemyDefinition magicResistant = Get(catalog, "magic-resistant");
            StealthEnemyDefinition stealth = Get<StealthEnemyDefinition>(catalog, "stealth");
            SpeedSupportEnemyDefinition speedSupport = Get<SpeedSupportEnemyDefinition>(catalog, "speed-support");
            ShortcutBuilderEnemyDefinition shortcutBuilder =
                Get<ShortcutBuilderEnemyDefinition>(catalog, "shortcut-builder");
            EnemyDefinition miniBoss = Get(catalog, "mini-boss");
            SummonerBossEnemyDefinition summonerBoss =
                Get<SummonerBossEnemyDefinition>(catalog, "summoner-boss");

            Assert.That(basic.GetType(), Is.EqualTo(typeof(EnemyDefinition)));
            Assert.That(armored.GetType(), Is.EqualTo(typeof(EnemyDefinition)));
            Assert.That(magicResistant.GetType(), Is.EqualTo(typeof(EnemyDefinition)));
            Assert.That(miniBoss.GetType(), Is.EqualTo(typeof(EnemyDefinition)));
            Assert.That(miniBoss.Rank, Is.EqualTo(EnemyRank.MiniBoss));

            Assert.That(stealth.RevealDurationSeconds, Is.EqualTo(2f));
            Assert.That(speedSupport.RegularSpeedBonusFraction, Is.EqualTo(0.25f));
            Assert.That(speedSupport.MiniBossSpeedBonusFraction, Is.EqualTo(0.10f));
            Assert.That(shortcutBuilder.ChannelDurationSeconds, Is.EqualTo(2.5f));
            Assert.That(miniBoss.PushDistanceMultiplier, Is.EqualTo(0.25f));
            Assert.That(summonerBoss.Rank, Is.EqualTo(EnemyRank.Boss));
            Assert.That(summonerBoss.PushDistanceMultiplier, Is.Zero);
            Assert.That(summonerBoss.SummonPhases, Has.Count.EqualTo(3));
            Assert.That(summonerBoss.SummonPhases[0].StartHealthFraction, Is.EqualTo(1f));
            Assert.That(summonerBoss.SummonPhases[1].StartHealthFraction, Is.EqualTo(0.6f));
            Assert.That(summonerBoss.SummonPhases[2].StartHealthFraction, Is.EqualTo(0.3f));
        }

        [Test]
        public void ApprovedEnemyCatalog_UsesOneValidViewPrefabPerDefinition()
        {
            EnemyCatalog catalog = AssetDatabase.LoadAssetAtPath<EnemyCatalog>(CatalogPath);
            var viewPrefabs = new HashSet<GameObject>();

            Assert.That(catalog, Is.Not.Null);
            foreach (EnemyDefinition definition in catalog.Definitions)
            {
                Assert.That(definition.ViewPrefab, Is.Not.Null, definition.StableId);
                Assert.That(
                    definition.ViewPrefab.GetComponent<EnemyView>(),
                    Is.Not.Null,
                    $"{definition.StableId} View Prefab must have EnemyView on its root.");
                Assert.That(
                    viewPrefabs.Add(definition.ViewPrefab),
                    Is.True,
                    $"{definition.StableId} must use its own View Prefab.");
            }
        }

        private static EnemyDefinition Get(EnemyCatalog catalog, string stableId)
        {
            Assert.That(catalog.TryGet(stableId, out EnemyDefinition definition), Is.True, stableId);
            return definition;
        }

        private static T Get<T>(EnemyCatalog catalog, string stableId) where T : EnemyDefinition
        {
            EnemyDefinition definition = Get(catalog, stableId);
            Assert.That(definition, Is.TypeOf<T>());
            return (T)definition;
        }
    }
}
