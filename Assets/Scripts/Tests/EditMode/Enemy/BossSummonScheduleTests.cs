using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.Enemies.Tests.EditMode
{
    public sealed class BossSummonScheduleTests
    {
        private const string BossPath = "Assets/Config/Enemies/SummonerBoss.asset";

        [Test]
        public void Build_SpreadsEverySummonAcrossTheCastWithoutOverrunningIt()
        {
            SummonerBossEnemyDefinition boss = LoadBoss();
            SummonerBossEnemyDefinition.SummonPhase phase = FindLargestPhase(boss);
            var schedule = new List<ScheduledSummon>();

            BossSummonSchedule.Build(boss, phase, 11L, 3, schedule);

            Assert.That(schedule.Count, Is.EqualTo(CountSummons(phase)));
            float previous = boss.SummonSpawnStartDelaySeconds;
            for (int index = 0; index < schedule.Count; index++)
            {
                Assert.That(
                    schedule[index].DueSeconds,
                    Is.GreaterThan(previous),
                    "Summons arrive one at a time, never together.");
                Assert.That(
                    schedule[index].DueSeconds,
                    Is.LessThan(boss.SummonSkillDurationSeconds),
                    "Every summon has to be out before the cast ends.");
                previous = schedule[index].DueSeconds;
                Assert.That(
                    Mathf.Abs(schedule[index].ForwardOffsetMeters),
                    Is.GreaterThan(boss.BaseHitRadius),
                    "A summon has to clear the boss's own body, never appear underneath it.");
            }
        }

        [Test]
        public void Build_SplitsTheSummonsEvenlyInFrontOfAndBehindTheBoss()
        {
            SummonerBossEnemyDefinition boss = LoadBoss();

            for (int phaseIndex = 0; phaseIndex < boss.SummonPhases.Count; phaseIndex++)
            {
                SummonerBossEnemyDefinition.SummonPhase phase = boss.SummonPhases[phaseIndex];
                var schedule = new List<ScheduledSummon>();
                BossSummonSchedule.Build(boss, phase, 11L + phaseIndex, 1, schedule);

                int ahead = 0;
                int behind = 0;
                for (int index = 0; index < schedule.Count; index++)
                {
                    if (schedule[index].ForwardOffsetMeters > 0f)
                    {
                        ahead++;
                    }
                    else
                    {
                        behind++;
                    }
                }

                Assert.That(
                    ahead,
                    Is.EqualTo(schedule.Count / 2),
                    "Phase " + phaseIndex + " must deal half its summons in front of the boss.");
                Assert.That(behind, Is.EqualTo(schedule.Count - ahead));
            }
        }

        [Test]
        public void Build_IsReplayableFromTheBossIdAndCastNumber()
        {
            SummonerBossEnemyDefinition boss = LoadBoss();
            SummonerBossEnemyDefinition.SummonPhase phase = FindLargestPhase(boss);
            var planned = new List<ScheduledSummon>();
            var replayed = new List<ScheduledSummon>();

            BossSummonSchedule.Build(boss, phase, 11L, 3, planned);
            BossSummonSchedule.Build(boss, phase, 11L, 3, replayed);

            // The timeline planner and the live simulation each build this on their own; if the
            // two ever disagreed the replay would spawn different enemies than were planned.
            Assert.That(replayed.Count, Is.EqualTo(planned.Count));
            for (int index = 0; index < planned.Count; index++)
            {
                Assert.That(replayed[index].Definition, Is.SameAs(planned[index].Definition));
                Assert.That(replayed[index].DueSeconds, Is.EqualTo(planned[index].DueSeconds));
                Assert.That(
                    replayed[index].ForwardOffsetMeters,
                    Is.EqualTo(planned[index].ForwardOffsetMeters));
            }
        }

        [Test]
        public void GetSpawnPlacement_PlacesSummonsAheadOfAndBehindTheBoss()
        {
            var route = new RoadPath(new[] { Vector3.zero, Vector3.right * 10f });
            var bossPosition = new Vector3(4f, 0f, 0f);

            BossSummonSchedule.GetSpawnPlacement(
                route, 1, bossPosition, 0.9f, out Vector3 ahead, out int aheadTarget);
            BossSummonSchedule.GetSpawnPlacement(
                route, 1, bossPosition, -0.9f, out Vector3 behind, out int behindTarget);

            Assert.That(ahead.x, Is.EqualTo(4.9f).Within(0.0001f));
            Assert.That(behind.x, Is.EqualTo(3.1f).Within(0.0001f));
            Assert.That(aheadTarget, Is.EqualTo(1));
            Assert.That(behindTarget, Is.EqualTo(1));
        }

        [Test]
        public void GetSpawnPlacement_WalksThroughCornersInsteadOfStoppingAtTheNextOne()
        {
            // Board roads put a waypoint every cell, so a summon placed a few meters ahead has to
            // cross several of them and take the waypoint it ends up heading for.
            var route = new RoadPath(new[]
            {
                Vector3.zero,
                new Vector3(1f, 0f, 0f),
                new Vector3(2f, 0f, 0f),
                new Vector3(3f, 0f, 0f),
                new Vector3(4f, 0f, 0f)
            });
            var bossPosition = new Vector3(0.5f, 0f, 0f);

            // 2.2m lands between waypoints on purpose: stopping exactly on one is ambiguous,
            // since the walker then reports the waypoint after it as the next target.
            BossSummonSchedule.GetSpawnPlacement(
                route, 1, bossPosition, 2.2f, out Vector3 ahead, out int aheadTarget);

            Assert.That(ahead.x, Is.EqualTo(2.7f).Within(0.0001f));
            Assert.That(aheadTarget, Is.EqualTo(3));
        }

        [Test]
        public void GetSpawnPlacement_StopsAtTheRoadStartWhenItCannotStepBackFurther()
        {
            var route = new RoadPath(new[]
            {
                Vector3.zero,
                new Vector3(1f, 0f, 0f),
                new Vector3(2f, 0f, 0f)
            });
            var bossPosition = new Vector3(0.4f, 0f, 0f);

            BossSummonSchedule.GetSpawnPlacement(
                route, 1, bossPosition, -3f, out Vector3 behind, out int behindTarget);

            Assert.That(behind.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(behindTarget, Is.EqualTo(1));
        }

        private static SummonerBossEnemyDefinition LoadBoss()
        {
            var boss = AssetDatabase.LoadAssetAtPath<SummonerBossEnemyDefinition>(BossPath);
            Assert.That(boss, Is.Not.Null, BossPath);
            return boss;
        }

        private static SummonerBossEnemyDefinition.SummonPhase FindLargestPhase(
            SummonerBossEnemyDefinition boss)
        {
            SummonerBossEnemyDefinition.SummonPhase largest = boss.SummonPhases[0];
            for (int index = 1; index < boss.SummonPhases.Count; index++)
            {
                if (CountSummons(boss.SummonPhases[index]) > CountSummons(largest))
                {
                    largest = boss.SummonPhases[index];
                }
            }

            return largest;
        }

        private static int CountSummons(SummonerBossEnemyDefinition.SummonPhase phase)
        {
            int total = 0;
            for (int index = 0; index < phase.Entries.Count; index++)
            {
                total += phase.Entries[index].Count;
            }

            return total;
        }
    }
}
