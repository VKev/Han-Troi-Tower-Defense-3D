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
        private const string MiniBossPath = "Assets/Config/Enemies/MiniBoss.asset";
        private const string BossPath = "Assets/Config/Enemies/SummonerBoss.asset";

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
                Assert.That(timeline.GetHeroAttacks(1L)[0].PrepareDurationSeconds, Is.EqualTo(0.6f));

                // Comfortably past the lunge, and still inside the first attack cycle of 40 ticks,
                // so exactly one impact has landed by here however the crab's timings are tuned.
                const long afterImpactTick = 30L;
                IReadOnlyList<PlannedEnemyFrame> frames = timeline.GetFrames(afterImpactTick);
                Assert.That(frames, Has.Count.EqualTo(2));
                Assert.That(frames[0].Health, Is.EqualTo(15f));
                Assert.That(frames[1].Health, Is.EqualTo(15f));
            }
            finally
            {
                manager.EndLevelSession();
            }
        }

        /// <summary>
        /// The strike holds what it catches, and the hold cannot be renewed until its gap has
        /// passed.
        /// </summary>
        /// <remarks>
        /// The gap is the whole of what keeps the hero from pinning a road down for good: it
        /// strikes every two seconds but holds for two and a half, so without it every strike
        /// would land on an already-held enemy and simply extend the hold forever. This asserts
        /// the enemy walks again exactly when the first hold expires, despite a second strike
        /// having landed while it was still held.
        /// </remarks>
        [Test]
        public void CrabHeroAttack_HoldsWhatItHitsAndCannotRenewTheHoldUntilTheGapPasses()
        {
            TowerCatalog catalog = AssetDatabase.LoadAssetAtPath<TowerCatalog>(TowerCatalogPath);
            ElementReactionCatalog reactions = AssetDatabase.LoadAssetAtPath<ElementReactionCatalog>(ReactionCatalogPath);
            EnemyDefinition basic = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(BasicEnemyPath);
            Assert.That(catalog.TryGet(TowerFamily.Hero, out TowerCombatDefinition heroDefinition), Is.True);
            var hero = (HeroTowerDefinition)heroDefinition;
            Assert.That(
                hero.StunDurationSeconds,
                Is.GreaterThan(0f),
                "This fixture only proves anything while the crab actually stuns.");

            var manager = new TowerNetworkManager(catalog);
            manager.BeginLevelSession(1);
            try
            {
                manager.RegisterTower(heroDefinition, new TowerWorldPosition(0f, 0f, 0f));
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
                    new WaveSpawnOrder(0f, basic, 0).WithEnemyId(1L)
                });

                long holdTicks = DurationTicks(manager, hero.StunDurationSeconds);
                long gapTicks = DurationTicks(manager, hero.StunImmunitySeconds);
                long cycleTicks = DurationTicks(manager, hero.Core.Throughput.CycleIntervalSeconds);
                Assert.That(
                    cycleTicks,
                    Is.LessThan(holdTicks),
                    "The crab has to strike again while its own hold is still running, or the gap "
                        + "is never put to the test.");

                long firstHoldTick = FindFirstHeldTick(timeline);
                Assert.That(firstHoldTick, Is.GreaterThan(0L), "The strike never held anything.");

                Vector3 heldPosition = FrameAt(timeline, firstHoldTick).Position;
                for (long tick = firstHoldTick; tick < firstHoldTick + holdTicks; tick++)
                {
                    PlannedEnemyFrame held = FrameAt(timeline, tick);
                    Assert.That(held.IsStunned, Is.True, $"Tick {tick} should still be held.");
                    Assert.That(
                        held.Position,
                        Is.EqualTo(heldPosition),
                        $"A held enemy must not advance, but it moved on tick {tick}.");
                }

                for (long tick = firstHoldTick + holdTicks;
                     tick < firstHoldTick + holdTicks + gapTicks;
                     tick++)
                {
                    Assert.That(
                        FrameAt(timeline, tick).IsStunned,
                        Is.False,
                        $"Tick {tick} falls in the gap, so no strike may hold the enemy again.");
                }

                Assert.That(
                    FrameAt(timeline, firstHoldTick + holdTicks + gapTicks - 1).Position.x,
                    Is.GreaterThan(heldPosition.x),
                    "The enemy has to walk again once the hold expires.");
            }
            finally
            {
                manager.EndLevelSession();
            }
        }

        /// <summary>
        /// A mini-boss is held for half as long as a regular enemy, the same half a knockback
        /// moves it.
        /// </summary>
        [Test]
        public void CrabHeroAttack_HoldsAMiniBossForHalfAsLong()
        {
            TowerCatalog catalog = AssetDatabase.LoadAssetAtPath<TowerCatalog>(TowerCatalogPath);
            ElementReactionCatalog reactions = AssetDatabase.LoadAssetAtPath<ElementReactionCatalog>(ReactionCatalogPath);
            EnemyDefinition miniBoss = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(MiniBossPath);
            Assert.That(miniBoss, Is.Not.Null);
            Assert.That(miniBoss.Rank, Is.EqualTo(EnemyRank.MiniBoss));
            Assert.That(catalog.TryGet(TowerFamily.Hero, out TowerCombatDefinition heroDefinition), Is.True);
            var hero = (HeroTowerDefinition)heroDefinition;

            var manager = new TowerNetworkManager(catalog);
            manager.BeginLevelSession(1);
            try
            {
                manager.RegisterTower(heroDefinition, new TowerWorldPosition(0f, 0f, 0f));
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
                    new WaveSpawnOrder(0f, miniBoss, 0).WithEnemyId(1L)
                });

                long firstHoldTick = FindFirstHeldTick(timeline);
                Assert.That(firstHoldTick, Is.GreaterThan(0L), "The strike never held the mini-boss.");

                long halfHoldTicks = DurationTicks(manager, hero.StunDurationSeconds * 0.5f);
                Assert.That(
                    FrameAt(timeline, firstHoldTick + halfHoldTicks - 1).IsStunned,
                    Is.True,
                    "The mini-boss has to stay held for its half of the hold.");
                Assert.That(
                    FrameAt(timeline, firstHoldTick + halfHoldTicks).IsStunned,
                    Is.False,
                    "The mini-boss must be released at half the hold, not at the full one.");
            }
            finally
            {
                manager.EndLevelSession();
            }
        }

        /// <summary>
        /// A boss is never held, however many times the strike lands on it.
        /// </summary>
        /// <remarks>
        /// It is the fight a level is built around, and a boss that spends it pinned in place is
        /// not a fight. It still takes the damage and still takes knockback - only the hold is
        /// refused.
        /// </remarks>
        [Test]
        public void CrabHeroAttack_NeverHoldsABoss()
        {
            TowerCatalog catalog = AssetDatabase.LoadAssetAtPath<TowerCatalog>(TowerCatalogPath);
            ElementReactionCatalog reactions = AssetDatabase.LoadAssetAtPath<ElementReactionCatalog>(ReactionCatalogPath);
            EnemyDefinition boss = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(BossPath);
            Assert.That(boss, Is.Not.Null);
            Assert.That(boss.Rank, Is.EqualTo(EnemyRank.Boss));
            Assert.That(catalog.TryGet(TowerFamily.Hero, out TowerCombatDefinition heroDefinition), Is.True);

            var manager = new TowerNetworkManager(catalog);
            manager.BeginLevelSession(1);
            try
            {
                manager.RegisterTower(heroDefinition, new TowerWorldPosition(0f, 0f, 0f));
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
                    new WaveSpawnOrder(0f, boss, 0).WithEnemyId(1L)
                });

                // Long enough for several strike cycles to have landed on it.
                Assert.That(
                    FindFirstHeldTick(timeline),
                    Is.EqualTo(-1L),
                    "The boss must never be held, not even for one tick.");

                Assert.That(
                    FrameAt(timeline, 30L).Health,
                    Is.LessThan(boss.BaseMaxHealth),
                    "The boss still has to be taking the strike's damage.");
            }
            finally
            {
                manager.EndLevelSession();
            }
        }

        private static long DurationTicks(TowerNetworkManager manager, float seconds)
        {
            return Mathf.Max(1, Mathf.CeilToInt(seconds / manager.TickSeconds));
        }

        /// <summary>First tick the planned enemy is held, or -1 if the plan never holds it.</summary>
        private static long FindFirstHeldTick(CombatTimeline timeline, long enemyId = 1L)
        {
            for (long tick = 1L; tick <= 600L; tick++)
            {
                if (TryGetFrame(timeline, tick, enemyId, out PlannedEnemyFrame frame) && frame.IsStunned)
                {
                    return tick;
                }
            }

            return -1L;
        }

        private static PlannedEnemyFrame FrameAt(CombatTimeline timeline, long tick, long enemyId = 1L)
        {
            Assert.That(
                TryGetFrame(timeline, tick, enemyId, out PlannedEnemyFrame frame),
                Is.True,
                $"No frame for enemy {enemyId} on tick {tick}.");
            return frame;
        }

        /// <summary>
        /// Looked up by id rather than taken from the head of the list: a boss puts summons of its
        /// own into the same frames, and they are not the enemy under test.
        /// </summary>
        private static bool TryGetFrame(
            CombatTimeline timeline,
            long tick,
            long enemyId,
            out PlannedEnemyFrame frame)
        {
            IReadOnlyList<PlannedEnemyFrame> frames = timeline.GetFrames(tick);
            for (int index = 0; index < frames.Count; index++)
            {
                if (frames[index].EnemyId == enemyId)
                {
                    frame = frames[index];
                    return true;
                }
            }

            frame = default;
            return false;
        }
    }
}
