using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TowerDefense3D.GridPlacement;
using UnityEngine;
using UnityEngine.TestTools;

namespace TowerDefense3D.Frog.Tests.PlayMode
{
    public sealed class FrogVictoryEscapePlayModeTests
    {
        private const string FrogPrefabResourcePath = "Prefabs/Frog";
        private const float JumpDurationSeconds = 0.35f;
        private const float PauseSeconds = 0.15f;
        private const float JumpDistanceMeters = 1.1f;
        private const int TouchdownFrame = 15;

        /// <summary>Frog_Jump runs frames 0-35, so the touchdown frame sits here in the clip.</summary>
        private const float TouchdownNormalizedTime = TouchdownFrame / 35f;

        /// <summary>
        /// The least slack a touchdown reading is allowed, for a frame rate high enough that three
        /// frames of the clip would be an unreasonably tight window.
        /// </summary>
        private const float TouchdownSlackFloor = 0.1f;

        private static readonly int JumpStateHash = Animator.StringToHash("Jump");
        private static readonly int IdleStateHash = Animator.StringToHash("Idle");
        private static readonly IEqualityComparer<Vector3> PositionComparer = new ApproximatePosition();

        /// <summary>
        /// The route runs (0,0,0)..(3,0,0) with the road painted on to (-3,0,0) past its start, and
        /// the frog begins on the far end, so 1.1m hops come down every 1.1m from x = 3 until the
        /// painted road runs out - carrying it well past the route start at the origin.
        /// </summary>
        private static readonly Vector3[] ExpectedLandings =
        {
            new Vector3(1.9f, 0f, 0f),
            new Vector3(0.8f, 0f, 0f),
            new Vector3(-0.3f, 0f, 0f),
            new Vector3(-1.4f, 0f, 0f),
            new Vector3(-2.5f, 0f, 0f)
        };

        /// <summary>
        /// The escape has to read as separate hops: the frog is either flying an arc or sitting
        /// still, never sliding along the road. A rest is a run of frames where the position does
        /// not move at all, so counting them counts the beats the player sees.
        /// </summary>
        [UnityTest]
        public IEnumerator PlayEscape_RestsOnEveryLandingPointBetweenHops()
        {
            EscapeFixture fixture = EscapeFixture.Create(new Vector3(3f, 0f, 0f));
            try
            {
                fixture.View.PlayEscape();

                yield return fixture.RunUntilEscapeCompleted();

                Assert.That(fixture.CompletedCount, Is.EqualTo(1), "Escape must report once.");
                AssertStartsWithPlannedLandings(
                    fixture.RestPositions,
                    "Every planned landing point must be a rest, taken in order.");
                Assert.That(
                    fixture.RestFrameCounts[0],
                    Is.GreaterThan(1),
                    "The frog must hold still between hops rather than jump continuously.");
            }
            finally
            {
                fixture.Dispose();
            }
        }

        /// <summary>
        /// One jump animation has one length, so every hop has to cover the same ground or that
        /// animation reads as a slide on the short ones. The range does not divide the route
        /// evenly, so the price is that the last hop carries the frog past the spawn - which is
        /// also what ends the escape.
        /// </summary>
        [UnityTest]
        public IEnumerator PlayEscape_MakesEveryHopTheFullJumpRangeAndClearsTheSpawn()
        {
            var start = new Vector3(3f, 0f, 0f);
            EscapeFixture fixture = EscapeFixture.Create(start);
            try
            {
                fixture.View.PlayEscape();

                yield return fixture.RunUntilEscapeCompleted();

                Assert.That(fixture.RestPositions, Is.Not.Empty);
                Vector3 previous = start;
                for (int index = 0; index < fixture.RestPositions.Count; index++)
                {
                    Vector3 landing = fixture.RestPositions[index];
                    Assert.That(
                        new Vector2(landing.x - previous.x, landing.z - previous.z).magnitude,
                        Is.EqualTo(JumpDistanceMeters).Within(0.0001f),
                        $"Hop {index} did not cover the full jump range.");
                    previous = landing;
                }

                Assert.That(
                    fixture.Frog.transform.position.x,
                    Is.LessThan(0f),
                    "The chain must carry the frog past the route start at the origin, along the "
                    + "road painted beyond it.");
            }
            finally
            {
                fixture.Dispose();
            }
        }

        /// <summary>
        /// The regression this guards: the Jump clip used to be restarted every hop while only its
        /// opening frames had played, so the frog looped a take-off forever and never landed. The
        /// clip is stretched to the hop instead, so one hop plays it through.
        /// </summary>
        [UnityTest]
        public IEnumerator PlayEscape_PlaysTheWholeJumpClipOncePerHopAndSettlesOnIdle()
        {
            EscapeFixture fixture = EscapeFixture.Create(new Vector3(2f, 0f, 0f));
            try
            {
                fixture.View.PlayEscape();

                yield return fixture.RunUntilEscapeCompleted();

                Assert.That(
                    fixture.PeakJumpNormalizedTime,
                    Is.GreaterThan(0.75f),
                    "One hop must play the Jump clip through to its landing frames.");
                Assert.That(
                    fixture.PeakJumpNormalizedTime,
                    Is.LessThan(1.5f),
                    "One hop must not run the Jump clip past its end and start over.");
                Assert.That(
                    fixture.SawIdleWhileResting,
                    Is.True,
                    "The frog must drop to Idle while it waits between hops.");
                Assert.That(
                    fixture.Animator.speed,
                    Is.EqualTo(1f).Within(0.0001f),
                    "The escape must hand the animator back at its authored speed.");
            }
            finally
            {
                fixture.Dispose();
            }
        }

        /// <summary>
        /// The arc is fitted to the animation, not the other way round: the frog must come down
        /// exactly as the clip reaches its touchdown frame, so the frames after it read as the
        /// landing of the hop that just happened rather than as a hop of their own.
        /// </summary>
        [UnityTest]
        public IEnumerator PlayEscape_ComesDownAsTheClipReachesItsTouchdownFrame()
        {
            EscapeFixture fixture = EscapeFixture.Create(new Vector3(3f, 0f, 0f));
            try
            {
                fixture.View.PlayEscape();

                yield return fixture.RunUntilEscapeCompleted();

                Assert.That(
                    fixture.TouchdownNormalizedTimes,
                    Has.Count.AtLeast(ExpectedLandings.Length),
                    "Every hop must report where in the clip its movement ended.");
                for (int index = 0; index < fixture.TouchdownNormalizedTimes.Count; index++)
                {
                    // Three frames of the clip, because the reading is late by up to two and the
                    // third is headroom. A hop timed to the wrong end of the clip misses by twenty
                    // frames, so this still catches what the test is for.
                    float slack = Mathf.Max(
                        TouchdownSlackFloor,
                        3f * fixture.TouchdownClipAdvances[index]);
                    Assert.That(
                        fixture.TouchdownNormalizedTimes[index],
                        Is.EqualTo(TouchdownNormalizedTime).Within(slack),
                        $"Hop {index} stopped moving at the wrong point in the Jump clip.");
                }
            }
            finally
            {
                fixture.Dispose();
            }
        }

        /// <summary>
        /// The escape must land on the road, not at whatever height the Cóc happens to be authored
        /// at. This starts the frog well above the road plane - the levels author it about a body
        /// above the ground - and every landing must still come down to road height.
        /// </summary>
        [UnityTest]
        public IEnumerator PlayEscape_LandsAtRoadHeightEvenWhenTheFrogStartsAboveIt()
        {
            EscapeFixture fixture = EscapeFixture.Create(new Vector3(3f, 2f, 0f));
            try
            {
                fixture.View.PlayEscape();

                yield return fixture.RunUntilEscapeCompleted();

                AssertStartsWithPlannedLandings(
                    fixture.RestPositions,
                    "The frog must come down onto the road rather than hover at its start height.");
                Assert.That(
                    fixture.Frog.transform.position.y,
                    Is.EqualTo(0f).Within(0.0001f),
                    "The frog must finish at road height.");
            }
            finally
            {
                fixture.Dispose();
            }
        }

        /// <summary>
        /// The victory panel waits on the frog being gone, not on the road being finished. With a
        /// camera watching only the near end of the road, the escape has to report itself partway
        /// along - while there are still landing points left in the chain.
        /// </summary>
        [UnityTest]
        public IEnumerator PlayEscape_ReportsAsSoonAsTheFrogLeavesTheScreen()
        {
            // A camera framing barely a metre of road around the frog's start, so it is out of shot
            // within a hop or two - long before the chain of landing points runs out.
            EscapeFixture fixture = EscapeFixture.Create(
                new Vector3(3f, 0f, 0f),
                new Vector3(3f, 0f, 0f),
                orthographicSize: 1f);
            try
            {
                fixture.View.PlayEscape();

                yield return fixture.RunUntilEscapeCompleted();

                Assert.That(fixture.CompletedCount, Is.EqualTo(1), "Escape must report once.");
                Assert.That(
                    fixture.RestPositions.Count,
                    Is.LessThan(ExpectedLandings.Length),
                    "The escape must end early, while the road still had landing points left.");
                Assert.That(
                    fixture.Frog.transform.position.x,
                    Is.LessThan(fixture.Camera.transform.position.x - fixture.Camera.orthographicSize),
                    "The frog must actually be outside the camera when the escape reports.");
            }
            finally
            {
                fixture.Dispose();
            }
        }

        /// <summary>
        /// The road's own landing points come first; the frog then keeps hopping past the end of
        /// the road until it is out of shot, so those planned points are a prefix rather than the
        /// whole list.
        /// </summary>
        private static void AssertStartsWithPlannedLandings(
            IReadOnlyList<Vector3> restPositions,
            string message)
        {
            Assert.That(restPositions, Has.Count.AtLeast(ExpectedLandings.Length), message);
            for (int index = 0; index < ExpectedLandings.Length; index++)
            {
                Assert.That(
                    restPositions[index],
                    Is.EqualTo(ExpectedLandings[index]).Using(PositionComparer),
                    message + " (landing " + index + ")");
            }
        }

        private sealed class ApproximatePosition : IEqualityComparer<Vector3>
        {
            public bool Equals(Vector3 left, Vector3 right)
            {
                return (left - right).sqrMagnitude < 0.0001f;
            }

            public int GetHashCode(Vector3 value)
            {
                return value.GetHashCode();
            }
        }

        /// <summary>
        /// One instantiated Frog on a straight four-cell road, tuned down to a fast hop so the test
        /// runs in about a second, plus the per-frame sampling the tests read.
        /// </summary>
        private sealed class EscapeFixture
        {
            /// <summary>
            /// Sampling runs on past the reported completion, because the escape reports itself as
            /// the frog comes down past the spawn while the landing animation is still playing.
            /// </summary>
            private const float SecondsAfterCompletion = 1.2f;

            private readonly List<int> restFrameCounts = new List<int>();
            private readonly List<Vector3> restPositions = new List<Vector3>();
            private readonly List<float> touchdownNormalizedTimes = new List<float>();
            private readonly List<float> touchdownClipAdvances = new List<float>();

            public GameObject Frog { get; private set; }
            public GameObject RoadObject { get; private set; }
            public GameObject CameraObject { get; private set; }
            public Camera Camera { get; private set; }
            public Animator Animator { get; private set; }
            public FrogVictoryEscapeView View { get; private set; }
            public int CompletedCount { get; private set; }
            public float PeakJumpNormalizedTime { get; private set; }
            public bool SawIdleWhileResting { get; private set; }
            public IReadOnlyList<Vector3> RestPositions => restPositions;
            public IReadOnlyList<int> RestFrameCounts => restFrameCounts;
            public IReadOnlyList<float> TouchdownNormalizedTimes => touchdownNormalizedTimes;

            /// <summary>
            /// How far the Jump clip moved over the frame each touchdown was sampled on. Where the
            /// clip stands can only be read once a frame, and the reading is a frame or two late by
            /// construction: the hop's last step overshoots its end, and the rest is not spotted
            /// until the frame after that. So the slack a touchdown is allowed has to be measured
            /// in rendered frames, not in a fixed slice of the clip - a fixed slice is several
            /// frames of headroom at a good frame rate and barely one during a hitch.
            /// </summary>
            public IReadOnlyList<float> TouchdownClipAdvances => touchdownClipAdvances;

            /// <summary>
            /// A camera framing the whole road and a little past its end, so the frog completes
            /// every planned landing and then leaves the shot a hop or two later.
            /// </summary>
            public static EscapeFixture Create(Vector3 startPosition)
            {
                return Create(startPosition, Vector3.zero, 4f);
            }

            /// <summary>
            /// The camera is created and handed to the view rather than left to Camera.main: other
            /// tests in the same Play Mode session leave cameras lying around, and whether the frog
            /// is on screen has to be this test's decision.
            /// </summary>
            public static EscapeFixture Create(
                Vector3 startPosition,
                Vector3 cameraCenter,
                float orthographicSize)
            {
                var prefab = Resources.Load<GameObject>(FrogPrefabResourcePath);
                Assert.That(prefab, Is.Not.Null, "Frog prefab must live in Resources.");

                var fixture = new EscapeFixture();
                fixture.Frog = Object.Instantiate(prefab);
                fixture.Frog.transform.position = startPosition;
                fixture.Animator = fixture.Frog.GetComponent<Animator>();
                fixture.View = fixture.Frog.GetComponent<FrogVictoryEscapeView>();
                Assert.That(fixture.Animator, Is.Not.Null);
                Assert.That(fixture.View, Is.Not.Null);

                SetField(fixture.View, "jumpDurationSeconds", JumpDurationSeconds);
                SetField(fixture.View, "pauseBetweenJumpsSeconds", PauseSeconds);
                SetField(fixture.View, "maximumJumpDistanceMeters", JumpDistanceMeters);
                SetIntField(fixture.View, "touchdownAnimationFrame", TouchdownFrame);

                fixture.RoadObject = BuildRoad();
                fixture.CameraObject = new GameObject("Escape Test Camera", typeof(Camera));
                var camera = fixture.CameraObject.GetComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = orthographicSize;

                // Forced so the visible half-width is exactly the orthographic size, whatever the
                // window the test runner happens to have.
                camera.aspect = 1f;
                camera.transform.position = new Vector3(cameraCenter.x, 20f, cameraCenter.z);
                camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                FindField(fixture.View, "worldCamera").SetValue(fixture.View, camera);
                fixture.Camera = camera;

                fixture.View.EscapeCompleted += fixture.HandleEscapeCompleted;
                return fixture;
            }

            public IEnumerator RunUntilEscapeCompleted()
            {
                // A hop costs its flight, its landing animation and the pause after it - close to a
                // second each here. This cap is deliberately far above what the longest chain needs
                // and only exists so a broken escape fails the assertions instead of hanging.
                const float budgetSeconds = 20f;
                float elapsedSeconds = 0f;
                float completedSeconds = 0f;
                Vector3 previousPosition = Frog.transform.position;

                // The coroutine takes a frame to start. Rests are only counted once the frog has
                // actually moved, so that slack is not mistaken for the first landing.
                bool hasMoved = false;
                int restRunLength = 0;
                float previousNormalizedTime = Animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                while ((CompletedCount == 0 && elapsedSeconds < budgetSeconds)
                    || (CompletedCount > 0 && completedSeconds < SecondsAfterCompletion))
                {
                    yield return null;
                    elapsedSeconds += Time.deltaTime;
                    if (CompletedCount > 0)
                    {
                        completedSeconds += Time.deltaTime;
                    }

                    AnimatorStateInfo state = Animator.GetCurrentAnimatorStateInfo(0);
                    Vector3 position = Frog.transform.position;
                    if (state.shortNameHash == JumpStateHash)
                    {
                        PeakJumpNormalizedTime = Mathf.Max(
                            PeakJumpNormalizedTime,
                            state.normalizedTime);
                    }

                    if ((position - previousPosition).sqrMagnitude < 0.0000001f)
                    {
                        if (hasMoved)
                        {
                            if (restRunLength == 0 && state.shortNameHash == JumpStateHash)
                            {
                                // First frame of a rest: the hop's movement has just ended, so
                                // this is where the clip stood when the frog touched down.
                                touchdownNormalizedTimes.Add(state.normalizedTime);
                                touchdownClipAdvances.Add(
                                    Mathf.Abs(state.normalizedTime - previousNormalizedTime));
                            }

                            restRunLength++;
                            SawIdleWhileResting |= state.shortNameHash == IdleStateHash;
                        }
                    }
                    else
                    {
                        hasMoved = true;
                        if (restRunLength > 0)
                        {
                            restFrameCounts.Add(restRunLength);
                            restPositions.Add(previousPosition);
                        }

                        restRunLength = 0;
                    }

                    previousPosition = position;
                    previousNormalizedTime = state.normalizedTime;
                }

                if (restRunLength > 0)
                {
                    restFrameCounts.Add(restRunLength);
                    restPositions.Add(previousPosition);
                }
            }

            public void Dispose()
            {
                if (View != null)
                {
                    View.EscapeCompleted -= HandleEscapeCompleted;
                }

                if (Frog != null)
                {
                    Object.Destroy(Frog);
                }

                if (CameraObject != null)
                {
                    Object.Destroy(CameraObject);
                }

                if (RoadObject != null)
                {
                    Object.Destroy(RoadObject);
                }
            }

            /// <summary>
            /// Road tiles laid one metre apart from x = 3 west to x = -3, the way a board lays them.
            ///
            /// The frog reads the road out of the scene, so the road has to actually be there. Half
            /// the tiles carry the authoring component and half do not, because that is how a real
            /// board comes out: straight tiles keep theirs, corners and junctions are swapped for
            /// meshes that have none and are known only by their name.
            /// </summary>
            private static GameObject BuildRoad()
            {
                var container = new GameObject("Generated Grid Placeables");
                for (int step = 0; step <= 6; step++)
                {
                    float x = 3f - step;
                    var tile = new GameObject("Road Cell (" + step + ")");
                    tile.transform.SetParent(container.transform, false);
                    tile.transform.position = new Vector3(x, 0f, 0f);
                    if (step % 2 != 0)
                    {
                        continue;
                    }

                    GridPlaceableAuthoring authoring = tile.AddComponent<GridPlaceableAuthoring>();
                    FindField(authoring, "displayName").SetValue(authoring, "Road");
                }

                return container;
            }

            private void HandleEscapeCompleted()
            {
                CompletedCount++;
            }

            private static void SetField(object target, string fieldName, float value)
            {
                FindField(target, fieldName).SetValue(target, value);
            }

            private static void SetIntField(object target, string fieldName, int value)
            {
                FindField(target, fieldName).SetValue(target, value);
            }

            private static FieldInfo FindField(object target, string fieldName)
            {
                FieldInfo field = target.GetType().GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null, $"Missing serialized field '{fieldName}'.");
                return field;
            }
        }
    }
}
