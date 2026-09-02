using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace TowerDefense3D.Enemies.Tests.EditMode
{
    public sealed class RoadJumpPathPlannerTests
    {
        /// <summary>Landings are computed, not echoed back, so they are compared within a tolerance.</summary>
        private static readonly IEqualityComparer<Vector3> PositionComparer = new ApproximatePosition();

        /// <summary>Off the west edge, which is where these boards run their road out to.</summary>
        private static readonly Func<Vector3, bool> OffScreenWestOfMinusOne = point => point.x < -1f;

        private static readonly Func<Vector3, bool> NeverOffScreen = _ => false;

        /// <summary>For the cases that are about the road itself rather than which spawn it leaves by.</summary>
        private static readonly Vector3[] NoSpawns = Array.Empty<Vector3>();

        [Test]
        public void FindShortestRoadPathOut_RunsFromTheTileNearestTheJumperToTheFirstTileOutOfShot()
        {
            List<Vector3> tiles = StraightRoad(-3f, 3f);

            List<Vector3> path = RoadJumpPathPlanner.FindShortestRoadPathOut(
                tiles,
                NoSpawns,
                new Vector3(1.4f, 5f, 0.2f),
                OffScreenWestOfMinusOne);

            Assert.That(path, Is.EqualTo(new[]
            {
                new Vector3(1f, 0f, 0f),
                new Vector3(0f, 0f, 0f),
                new Vector3(-1f, 0f, 0f),
                new Vector3(-2f, 0f, 0f)
            }).Using(PositionComparer),
                "It must start on the nearest tile and stop on the first tile out of shot.");
        }

        /// <summary>
        /// The failure this exists for: the road forks and the jumper took whichever branch the
        /// level drew, which on some boards was the long way round.
        /// </summary>
        [Test]
        public void FindShortestRoadPathOut_TakesTheShortBranchOfAFork()
        {
            var tiles = new List<Vector3>
            {
                new Vector3(0f, 0f, 0f)
            };

            // Short branch: three tiles west, out of shot at x = -2.
            tiles.Add(new Vector3(-1f, 0f, 0f));
            tiles.Add(new Vector3(-2f, 0f, 0f));

            // Long branch: south then a long way west, also ending out of shot.
            for (int step = 1; step <= 6; step++)
            {
                tiles.Add(new Vector3(0f, 0f, -step));
            }

            for (int step = 1; step <= 6; step++)
            {
                tiles.Add(new Vector3(-step, 0f, -6f));
            }

            List<Vector3> path = RoadJumpPathPlanner.FindShortestRoadPathOut(
                tiles,
                NoSpawns,
                Vector3.zero,
                OffScreenWestOfMinusOne);

            Assert.That(path, Is.EqualTo(new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(-1f, 0f, 0f),
                new Vector3(-2f, 0f, 0f)
            }).Using(PositionComparer),
                "The short way out must win, whichever branch a route would have walked.");
        }

        [Test]
        public void FindShortestRoadPathOut_GoesAsFarAsTheRoadAllowsWhenNothingIsOutOfShot()
        {
            List<Vector3> tiles = StraightRoad(-3f, 3f);

            List<Vector3> path = RoadJumpPathPlanner.FindShortestRoadPathOut(
                tiles,
                NoSpawns,
                new Vector3(3f, 0f, 0f),
                NeverOffScreen);

            Assert.That(
                path[path.Count - 1],
                Is.EqualTo(new Vector3(-3f, 0f, 0f)).Using(PositionComparer),
                "A board whose road never leaves the camera must still send the jumper to its end.");
        }

        [Test]
        public void FindShortestRoadPathOut_WillNotCrossAGapInTheRoad()
        {
            var tiles = new List<Vector3>
            {
                new Vector3(2f, 0f, 0f),
                new Vector3(1f, 0f, 0f),

                // Four cells away: a separate stretch of road, not a continuation of this one.
                new Vector3(-3f, 0f, 0f),
                new Vector3(-4f, 0f, 0f)
            };

            List<Vector3> path = RoadJumpPathPlanner.FindShortestRoadPathOut(
                tiles,
                NoSpawns,
                new Vector3(2f, 0f, 0f),
                OffScreenWestOfMinusOne);

            Assert.That(
                path,
                Is.EqualTo(new[] { new Vector3(2f, 0f, 0f), new Vector3(1f, 0f, 0f) })
                    .Using(PositionComparer),
                "The jumper must not teleport across to an unconnected road.");
        }

        /// <summary>
        /// Diagonally touching tiles are connected, so the way round a bend cuts its corner rather
        /// than stepping through it. That is both the shorter path and the one that matches how the
        /// jumper flies it - a hop's chord goes straight while the road turns.
        /// </summary>
        [Test]
        public void FindShortestRoadPathOut_CutsTheCornerOfABend()
        {
            var tiles = new List<Vector3>
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 0f, 1f),
                new Vector3(0f, 0f, 2f),
                new Vector3(-1f, 0f, 2f),
                new Vector3(-2f, 0f, 2f)
            };

            List<Vector3> path = RoadJumpPathPlanner.FindShortestRoadPathOut(
                tiles,
                NoSpawns,
                Vector3.zero,
                OffScreenWestOfMinusOne);

            Assert.That(path, Is.EqualTo(new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 0f, 1f),
                new Vector3(-1f, 0f, 2f),
                new Vector3(-2f, 0f, 2f)
            }).Using(PositionComparer),
                "The corner tile is skipped diagonally, which is the shorter way round.");

            for (int index = 1; index < path.Count; index++)
            {
                Assert.That(
                    DistanceOnGround(path[index - 1], path[index]),
                    Is.LessThanOrEqualTo(1.6f),
                    "Every step must stay between tiles that actually touch.");
            }
        }

        /// <summary>
        /// The way out is a Road Spawn, so of two branches that both leave the shot the one with a
        /// spawn on it wins - even when it is the longer of the two.
        /// </summary>
        [Test]
        public void FindShortestRoadPathOut_LeavesByTheNearestSpawnRatherThanTheNearestEdge()
        {
            var tiles = new List<Vector3> { Vector3.zero };

            // West: two tiles, out of shot at x = -2, but no spawn on it.
            tiles.Add(new Vector3(-1f, 0f, 0f));
            tiles.Add(new Vector3(-2f, 0f, 0f));

            // North: four tiles, with the spawn on the last of them.
            for (int step = 1; step <= 4; step++)
            {
                tiles.Add(new Vector3(0f, 0f, step));
            }

            var spawns = new[] { new Vector3(0f, 0f, 4f) };

            List<Vector3> path = RoadJumpPathPlanner.FindShortestRoadPathOut(
                tiles,
                spawns,
                Vector3.zero,
                point => point.x < -1f || point.z > 3f);

            Assert.That(path, Is.EqualTo(new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 0f, 1f),
                new Vector3(0f, 0f, 2f),
                new Vector3(0f, 0f, 3f),
                new Vector3(0f, 0f, 4f)
            }).Using(PositionComparer),
                "It must head for the spawn, not for whichever edge happens to be closest.");
        }

        /// <summary>
        /// The spawn is where the road leaves the board, not where the frog stops: it carries on
        /// past it, still on the same road, until it is out of shot.
        /// </summary>
        [Test]
        public void FindShortestRoadPathOut_CarriesOnPastTheSpawnUntilOutOfShot()
        {
            List<Vector3> tiles = StraightRoad(-4f, 2f);
            var spawns = new[] { new Vector3(-1f, 0f, 0f) };

            List<Vector3> path = RoadJumpPathPlanner.FindShortestRoadPathOut(
                tiles,
                spawns,
                new Vector3(2f, 0f, 0f),
                point => point.x < -2.5f);

            Assert.That(
                path[path.Count - 1],
                Is.EqualTo(new Vector3(-3f, 0f, 0f)).Using(PositionComparer),
                "The path must run through the spawn at -1 and on to the first tile out of shot.");
            Assert.That(
                path,
                Has.Some.EqualTo(spawns[0]).Using(PositionComparer),
                "And it must actually pass through the spawn on the way.");
        }

        [Test]
        public void FindShortestRoadPathOut_StopsAtTheSpawnWhenTheRoadEndsThere()
        {
            List<Vector3> tiles = StraightRoad(0f, 3f);
            var spawns = new[] { Vector3.zero };

            List<Vector3> path = RoadJumpPathPlanner.FindShortestRoadPathOut(
                tiles,
                spawns,
                new Vector3(3f, 0f, 0f),
                NeverOffScreen);

            Assert.That(
                path[path.Count - 1],
                Is.EqualTo(Vector3.zero).Using(PositionComparer),
                "With no road past the spawn the escape still reads as leaving by it.");
        }

        [Test]
        public void FindShortestRoadPathOut_ReturnsNothingWithoutAnyRoad()
        {
            Assert.That(
                RoadJumpPathPlanner.FindShortestRoadPathOut(
                    Array.Empty<Vector3>(),
                    NoSpawns,
                    Vector3.zero,
                    NeverOffScreen),
                Is.Empty);
        }

        [Test]
        public void SpaceEvenly_StopsOnTheEndWhenTheRangeDividesTheRoadExactly()
        {
            List<Vector3> guide = StraightGuide(3f, 0f);

            List<Vector3> landings = RoadJumpPathPlanner.SpaceEvenly(
                new Vector3(3f, 0f, 0f),
                guide,
                jumpDistanceMeters: 1.5f);

            Assert.That(
                landings,
                Is.EqualTo(new[] { new Vector3(1.5f, 0f, 0f), Vector3.zero }).Using(PositionComparer),
                "A hop that comes down on the last tile must end the chain there.");
        }

        [Test]
        public void SpaceEvenly_StopsOnTheLastFullHopThatStillFitsOnRoad()
        {
            List<Vector3> landings = RoadJumpPathPlanner.SpaceEvenly(
                new Vector3(3f, 0f, 0f),
                StraightGuide(3f, 0f),
                jumpDistanceMeters: 1.1f);

            Assert.That(landings, Is.EqualTo(new[]
            {
                new Vector3(1.9f, 0f, 0f),
                new Vector3(0.8f, 0f, 0f)
            }).Using(PositionComparer),
                "0.8m of road is left over, which is less than a hop, so it is not travelled "
                + "rather than paid for with a short hop or a landing off the road.");
        }

        /// <summary>
        /// The point of the whole exercise: one jump animation, one hop length.
        /// </summary>
        [Test]
        public void SpaceEvenly_KeepsEveryHopAtTheFullRangeAroundACorner()
        {
            var start = new Vector3(2f, 0f, 2f);
            List<Vector3> guide = CorneredGuide();
            const float jumpDistanceMeters = 1.2f;

            List<Vector3> landings = RoadJumpPathPlanner.SpaceEvenly(start, guide, jumpDistanceMeters);

            Assert.That(landings, Is.Not.Empty);
            Vector3 previous = start;
            for (int index = 0; index < landings.Count; index++)
            {
                Assert.That(
                    DistanceOnGround(previous, landings[index]),
                    Is.EqualTo(jumpDistanceMeters).Within(0.0001f),
                    $"Hop {index} is not the full range.");
                previous = landings[index];
            }
        }

        /// <summary>
        /// The constraint that outranks uniform hops: a hop's chord may fly over anything, but the
        /// landing has to be back on the road.
        /// </summary>
        [Test]
        public void SpaceEvenly_LandsOnTheRoadEvenWhereTheHopCutsTheCorner()
        {
            List<Vector3> guide = CorneredGuide();

            List<Vector3> landings = RoadJumpPathPlanner.SpaceEvenly(
                new Vector3(2f, 0f, 2f),
                guide,
                jumpDistanceMeters: 1.2f);

            Assert.That(landings.Count, Is.GreaterThan(1));
            for (int index = 0; index < landings.Count; index++)
            {
                Assert.That(
                    DistanceToGuide(guide, landings[index]),
                    Is.LessThan(0.0001f),
                    $"Landing {index} at {landings[index]} left the road.");
            }

            Vector3 cornerCuttingHop = landings[1] - landings[0];
            Assert.That(
                DistanceToGuide(guide, landings[0] + cornerCuttingHop * 0.5f),
                Is.GreaterThan(0.0001f),
                "This road bends hard enough that a full-range hop must fly off it in between, "
                + "which is what makes the landing check meaningful.");
        }

        [Test]
        public void SpaceEvenly_TakesTheRoadHeightRatherThanTheJumpersHeight()
        {
            List<Vector3> landings = RoadJumpPathPlanner.SpaceEvenly(
                new Vector3(3f, 9f, 0f),
                StraightGuide(2f, 3f),
                jumpDistanceMeters: 1.5f);

            Assert.That(landings, Is.Not.Empty);
            for (int index = 0; index < landings.Count; index++)
            {
                Assert.That(landings[index].y, Is.EqualTo(3f).Within(0.0001f));
            }
        }

        [Test]
        public void SpaceEvenly_GetsAFarJumperOntoTheRoadBeforeSpacingTheRest()
        {
            List<Vector3> landings = RoadJumpPathPlanner.SpaceEvenly(
                new Vector3(3f, 0f, 20f),
                StraightGuide(3f, 0f),
                jumpDistanceMeters: 1.5f);

            Assert.That(
                landings,
                Is.EqualTo(new[]
                {
                    new Vector3(3f, 0f, 0f),
                    new Vector3(1.5f, 0f, 0f),
                    Vector3.zero
                }).Using(PositionComparer),
                "A frog stranded off the road must land on it rather than short of it.");
        }

        [Test]
        public void SpaceEvenly_ReturnsNothingForAnEmptyGuideOrANonPositiveRange()
        {
            Assert.That(
                RoadJumpPathPlanner.SpaceEvenly(Vector3.right, Array.Empty<Vector3>(), 1f),
                Is.Empty);
            Assert.That(
                RoadJumpPathPlanner.SpaceEvenly(Vector3.right, StraightGuide(2f, 0f), 0f),
                Is.Empty);
        }

        /// <summary>Road tiles one metre apart along the x axis, west-most first.</summary>
        private static List<Vector3> StraightRoad(float fromX, float toX)
        {
            var tiles = new List<Vector3>();
            for (float x = fromX; x <= toX; x += 1f)
            {
                tiles.Add(new Vector3(x, 0f, 0f));
            }

            return tiles;
        }

        /// <summary>Road cells one metre apart running west to the origin.</summary>
        private static List<Vector3> StraightGuide(float fromX, float height)
        {
            var guide = new List<Vector3>();
            for (float x = fromX; x >= 0f; x -= 1f)
            {
                guide.Add(new Vector3(x, height, 0f));
            }

            return guide;
        }

        /// <summary>Road cells running south then west, so the guide turns a right angle.</summary>
        private static List<Vector3> CorneredGuide()
        {
            return new List<Vector3>
            {
                new Vector3(2f, 0f, 2f),
                new Vector3(2f, 0f, 1f),
                new Vector3(2f, 0f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(0f, 0f, 0f)
            };
        }

        /// <summary>How far a point sits off the polyline the guide draws down the road.</summary>
        private static float DistanceToGuide(IReadOnlyList<Vector3> guide, Vector3 point)
        {
            float nearest = float.MaxValue;
            for (int index = 1; index < guide.Count; index++)
            {
                nearest = Mathf.Min(nearest, DistanceToSegment(guide[index - 1], guide[index], point));
            }

            return nearest;
        }

        private static float DistanceToSegment(Vector3 from, Vector3 to, Vector3 point)
        {
            var segment = new Vector2(to.x - from.x, to.z - from.z);
            var offset = new Vector2(point.x - from.x, point.z - from.z);
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= 0f)
            {
                return offset.magnitude;
            }

            float travel = Mathf.Clamp01(Vector2.Dot(offset, segment) / lengthSquared);
            return (offset - segment * travel).magnitude;
        }

        private static float DistanceOnGround(Vector3 from, Vector3 to)
        {
            return new Vector2(to.x - from.x, to.z - from.z).magnitude;
        }

        private sealed class ApproximatePosition : IEqualityComparer<Vector3>
        {
            public bool Equals(Vector3 left, Vector3 right)
            {
                return (left - right).sqrMagnitude < 0.000001f;
            }

            public int GetHashCode(Vector3 value)
            {
                return value.GetHashCode();
            }
        }
    }
}
