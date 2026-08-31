using System.Collections.Generic;
using NUnit.Framework;
using TowerDefense3D.Towers;
using TowerDefense3D.Waves;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.Enemies.Tests.EditMode
{
    public sealed class HeroAttackTimelineTests
    {
        private const string TowerCatalogPath = "Assets/Config/Towers/Catalogs/TowerCatalog.asset";
        private const string ReactionCatalogPath = "Assets/Config/Combat/ElementReactionCatalog.asset";
        private const string BasicEnemyPath = "Assets/Config/Enemies/Basic.asset";

        [Test]
        public void CrabHeroAttack_HitsLeadingEnemyAndNearbyEnemiesAtImpact()
        {
            TowerCatalog catalog = AssetDatabase.LoadAssetAtPath<TowerCatalog>(TowerCatalogPath);
            ElementReactionCatalog reactions = AssetDatabase.LoadAssetAtPath<ElementReactionCatalog>(ReactionCatalogPath);
            EnemyDefinition basic = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(BasicEnemyPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(reactions, Is.Not.Null);
            Assert.That(basic, Is.Not.Null);
            Assert.That(catalog.TryGet(TowerFamily.Hero, out TowerCombatDefinition heroDefinition), Is.True);
            Assert.That(catalog.TryGet(TowerFamily.Generator, out TowerCombatDefinition generatorDefinition), Is.True);
            Assert.That(catalog.TryGet(TowerFamily.SoulNexus, out TowerCombatDefinition nexusDefinition), Is.True);

            var manager = new TowerNetworkManager(catalog);
            manager.BeginLevelSession(1);
            try
            {
                TowerNodeId hero = manager.RegisterTower(heroDefinition, new TowerWorldPosition(0f, 0f, 0f));
                TowerNodeId generator = manager.RegisterTower(generatorDefinition, new TowerWorldPosition(8f, 0f, 0f));
                TowerNodeId nexus = manager.RegisterTower(nexusDefinition, new TowerWorldPosition(9f, 0f, 0f));
                Assert.That(manager.TryRewire(generator, nexus, out string linkError), Is.True, linkError);
                Assert.That(manager.TryStartSimulation(out string startError), Is.True, startError);

                var planner = new CombatTimelinePlanner(
                    manager,
                    new RoadPathSet(new[]
                    {
                        new RoadPath(new[] { new Vector3(-2f, 0f, 0f), new Vector3(10f, 0f, 0f) })
                    }),
                    reactions);
                CombatTimeline timeline = planner.Create(new List<WaveSpawnOrder>
                {
                    new WaveSpawnOrder(0f, basic, 0).WithEnemyId(1L),
                    new WaveSpawnOrder(0f, basic, 1).WithEnemyId(2L)
                });

                Assert.That(timeline.GetHeroAttacks(1L), Has.Count.EqualTo(1));
                Assert.That(timeline.GetHeroAttacks(1L)[0].TowerNodeId, Is.EqualTo(hero));
                Assert.That(timeline.GetHeroAttacks(1L)[0].PrepareDurationSeconds, Is.EqualTo(1f));

                const long impactTick = 30L;
                IReadOnlyList<PlannedEnemyFrame> frames = timeline.GetFrames(impactTick);
                Assert.That(frames, Has.Count.EqualTo(2));
                Assert.That(frames[0].Health, Is.EqualTo(2f));
                Assert.That(frames[1].Health, Is.EqualTo(2f));
            }
            finally
            {
                manager.EndLevelSession();
            }
        }
    }
}
