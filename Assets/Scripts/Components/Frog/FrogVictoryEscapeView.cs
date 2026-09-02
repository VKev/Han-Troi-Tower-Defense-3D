using System;
using System.Collections;
using System.Collections.Generic;
using TowerDefense3D.Enemies;
using TowerDefense3D.GameFlow;
using TowerDefense3D.GridPlacement;
using UnityEngine;

namespace TowerDefense3D.Frog
{
    /// <summary>
    /// The Cóc's victory exit: once every wave is cleared it hops off the board along a precomputed
    /// chain of landing points, out through the nearest Road Spawn, then reports the escape as
    /// finished so the outcome HUD can raise its victory panel.
    ///
    /// The chain is planned up front over the road tiles themselves - the shortest way out through
    /// a Road Spawn - and not cell by cell: a hop's chord flies straight while the road bends, so
    /// the frog cuts corners, and only its landing points have to be back on the road.
    ///
    /// One hop is one discrete beat, and the arc is fitted to the animation rather than the other
    /// way round. The Jump clip carries the frog forward and up to its touchdown frame and brings
    /// it back down over the frames after it, so the travel is timed to arrive exactly on that
    /// frame; the rest of the clip then plays out over the point the frog arrived at, and only
    /// after that does it settle onto Idle. The clip is never scrubbed or restarted from code - it
    /// plays once, straight through, and the movement is what bends to match it.
    ///
    /// Landing points are taken at road height, the same height enemies walk at, rather than at
    /// the height the Cóc happens to be authored at. Carrying that authored height along left the
    /// frog hovering a body above the road for the whole escape.
    ///
    /// Every hop is the full jump range. There is one jump animation of one length, so a short hop
    /// would play it over too little ground and read as a slide; the road path is re-cut into equal
    /// full-range steps instead.
    ///
    /// The escape ends when the frog is no longer on screen - checked mid-flight, because that is
    /// the moment the player stops having anything to watch. Most boards stop laying road well
    /// short of the camera edge, so once the road runs out the frog keeps hopping along the heading
    /// it left on until it is out of shot.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class FrogVictoryEscapeView : MonoBehaviour, ILevelVictoryEscapeView
    {
        private static readonly int JumpState = Animator.StringToHash("Jump");
        private static readonly int IdleState = Animator.StringToHash("Idle");

        [SerializeField, Min(0.01f)]
        [Tooltip("Airborne time of one hop: how long the frog takes to travel its arc and come "
            + "down on the next landing point. Every hop covers the same distance, so this one "
            + "value fits them all. The frames after the touchdown frame play on top of it, so a "
            + "whole hop lasts longer than this value.")]
        private float jumpDurationSeconds = 0.4f;

        [SerializeField, Min(0f)]
        [Tooltip("Extra height added on top of the arc the Jump clip already animates, peaking "
            + "mid-hop. Zero leaves the whole arc to the animation; it never changes the height "
            + "the frog lands at.")]
        private float jumpHeightMeters = 0.45f;

        [SerializeField, Min(1)]
        [Tooltip("The frame of the Jump clip where the frog touches down. Everything before it is "
            + "the leap, so the arc is timed to finish exactly here; everything after it is the "
            + "landing, and plays where the frog came down.")]
        private int touchdownAnimationFrame = 15;

        [SerializeField, Min(0f)]
        [Tooltip("Extra Idle time after the landing animation finishes, before the next hop. The "
            + "landing frames already read as a stop, so this only lengthens that beat.")]
        private float pauseBetweenJumpsSeconds = 0.15f;

        [SerializeField, Min(0.1f)]
        [Tooltip("Length of every hop. The road path is re-cut into steps of exactly this, so a "
            + "shorter range means more hops, not shorter ones.")]
        private float maximumJumpDistanceMeters = 2.5f;

        /// <summary>
        /// Hops taken after the road runs out before giving up on leaving the screen. Purely a
        /// runaway guard - with a camera watching, the frog is out of shot in a few hops.
        /// </summary>
        private const int MaximumHopsPastTheRoad = 24;

        private readonly Plane[] frustumPlanes = new Plane[6];

        private Animator animator;
        private Renderer[] renderers;
        private Camera worldCamera;
        private Vector3 lastHeading = Vector3.forward;
        private Coroutine escapeSequence;
        private bool hasReportedEscape;
        private float jumpClipLengthSeconds;
        private float jumpClipFrameRate;

        public event Action EscapeCompleted;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            renderers = GetComponentsInChildren<Renderer>(true);
            ReadJumpClip();
        }

        private void OnDisable()
        {
            if (escapeSequence != null)
            {
                StopCoroutine(escapeSequence);
                escapeSequence = null;
            }

            animator.speed = 1f;
        }

        public void PlayEscape()
        {
            if (escapeSequence != null)
            {
                return;
            }

            List<Vector3> roadTiles = RoadTileLocator.CollectWorldPositions();
            if (roadTiles.Count == 0)
            {
                EscapeCompleted?.Invoke();
                return;
            }

            List<Vector3> guide = RoadJumpPathPlanner.FindShortestRoadPathOut(
                roadTiles,
                RoadSpawnLocator.CollectWorldPositions(),
                transform.position,
                IsOffScreen);
            escapeSequence = StartCoroutine(JumpThrough(
                RoadJumpPathPlanner.SpaceEvenly(
                    transform.position,
                    guide,
                    maximumJumpDistanceMeters)));
        }

        private IEnumerator JumpThrough(List<Vector3> landings)
        {
            for (int index = 0; index < landings.Count && !hasReportedEscape; index++)
            {
                if (index > 0 && pauseBetweenJumpsSeconds > 0f)
                {
                    yield return new WaitForSeconds(pauseBetweenJumpsSeconds);
                }

                yield return Leap(landings[index]);
                yield return Land();
            }

            // The road has run out. Most boards stop laying it well short of the camera edge, and
            // the escape is only over once the player can no longer see the frog, so it carries on
            // along the heading it left the road on. Without a camera there is no way to know when
            // that is, so the panel goes up where it stands instead.
            if (GetWorldCamera() != null)
            {
                for (int hop = 0; hop < MaximumHopsPastTheRoad && !hasReportedEscape; hop++)
                {
                    if (pauseBetweenJumpsSeconds > 0f)
                    {
                        yield return new WaitForSeconds(pauseBetweenJumpsSeconds);
                    }

                    yield return Leap(transform.position + lastHeading * maximumJumpDistanceMeters);
                    yield return Land();
                }
            }

            ReportEscaped();
        }

        /// <summary>The arc: take off, fly, come down exactly on the touchdown frame.</summary>
        private IEnumerator Leap(Vector3 destination)
        {
            Vector3 start = transform.position;
            Vector3 direction = destination - start;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                lastHeading = direction.normalized;
                transform.rotation = Quaternion.LookRotation(lastHeading, Vector3.up);
            }

            // Nothing else drives this Animator during the escape, so scaling it whole is the
            // shortest way to line the clip up with the arc: the leap - clip start to touchdown
            // frame - is stretched to the airborne time, which fixes the speed of the landing
            // frames after it too.
            float touchdownClipSeconds = GetTouchdownClipSeconds();
            float clipSpeed = touchdownClipSeconds > 0f
                ? touchdownClipSeconds / jumpDurationSeconds
                : 1f;
            animator.speed = clipSpeed;
            animator.Play(JumpState, 0, 0f);

            float elapsedSeconds = 0f;
            while (elapsedSeconds < jumpDurationSeconds)
            {
                elapsedSeconds += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsedSeconds / jumpDurationSeconds);
                Vector3 position = Vector3.Lerp(start, destination, progress);
                position.y += Mathf.Sin(progress * Mathf.PI) * jumpHeightMeters;
                transform.position = position;

                // Checked mid-flight, not on landing: the escape is over the moment the player can
                // no longer see the frog, and waiting for it to come down would hold the victory
                // panel back for a hop the player never sees. The hop is still flown out to its
                // landing behind the panel, so the frog is never left hanging in mid-air.
                if (HasLeftTheScreen())
                {
                    ReportEscaped();
                }

                yield return null;
            }

            transform.position = destination;
        }

        /// <summary>
        /// The frames after touchdown, played where the frog came down so the animation finishes
        /// the hop rather than being cut off by the next one.
        /// </summary>
        private IEnumerator Land()
        {
            float clipSpeed = animator.speed;
            float landingSeconds = clipSpeed > 0f
                ? (jumpClipLengthSeconds - GetTouchdownClipSeconds()) / clipSpeed
                : 0f;
            if (landingSeconds > 0f)
            {
                yield return new WaitForSeconds(landingSeconds);
            }

            animator.speed = 1f;
            animator.Play(IdleState, 0, 0f);
        }

        /// <summary>
        /// True once nothing the frog draws is inside the camera any more. Its own renderer bounds
        /// are used rather than the pivot, so the panel waits for the whole frog to clear the edge
        /// instead of going up while half of it is still in shot.
        /// </summary>
        private bool HasLeftTheScreen()
        {
            return IsOutsideCamera(GetFrogBounds());
        }

        /// <summary>
        /// Whether a frog standing on this point would be out of shot. Used to pick where the road
        /// path should come out, so it is the frog's own size that is tested, not the bare point:
        /// a spot whose centre is just off the edge still shows most of the frog.
        /// </summary>
        private bool IsOffScreen(Vector3 point)
        {
            Bounds bounds = GetFrogBounds();
            bounds.center = point + (bounds.center - transform.position);
            return IsOutsideCamera(bounds);
        }

        private bool IsOutsideCamera(Bounds bounds)
        {
            Camera camera = GetWorldCamera();
            if (camera == null || renderers.Length == 0)
            {
                return false;
            }

            GeometryUtility.CalculateFrustumPlanes(camera, frustumPlanes);
            return !GeometryUtility.TestPlanesAABB(frustumPlanes, bounds);
        }

        private Bounds GetFrogBounds()
        {
            if (renderers.Length == 0)
            {
                return new Bounds(transform.position, Vector3.zero);
            }

            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            return bounds;
        }

        /// <summary>
        /// The camera the level is played through. Resolved lazily because the escape only runs
        /// once, at the end of a level, by which time the level camera is long since up.
        /// </summary>
        private Camera GetWorldCamera()
        {
            if (worldCamera == null)
            {
                worldCamera = Camera.main != null
                    ? Camera.main
                    : FindFirstObjectByType<Camera>();
            }

            return worldCamera;
        }

        /// <summary>
        /// Reports the escape once. Both endings - leaving the screen and running out of road -
        /// funnel through here, and the outcome HUD must only be told the once.
        /// </summary>
        private void ReportEscaped()
        {
            if (hasReportedEscape)
            {
                return;
            }

            hasReportedEscape = true;
            escapeSequence = null;
            EscapeCompleted?.Invoke();
        }

        /// <summary>
        /// Where the touchdown frame falls in the Jump clip, in unscaled clip seconds. Zero when
        /// there is no jump clip to measure, which leaves the animator at its authored speed.
        /// </summary>
        private float GetTouchdownClipSeconds()
        {
            if (jumpClipLengthSeconds <= 0f || jumpClipFrameRate <= 0f)
            {
                return 0f;
            }

            return Mathf.Min(touchdownAnimationFrame / jumpClipFrameRate, jumpClipLengthSeconds);
        }

        /// <summary>
        /// The Jump state's clip, found by name because this controller holds one clip per pose and
        /// the state names are the ones this component already plays.
        /// </summary>
        private void ReadJumpClip()
        {
            RuntimeAnimatorController controller = animator.runtimeAnimatorController;
            if (controller == null)
            {
                return;
            }

            AnimationClip[] clips = controller.animationClips;
            for (int index = 0; index < clips.Length; index++)
            {
                if (clips[index] != null
                    && clips[index].name.IndexOf("Jump", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    jumpClipLengthSeconds = clips[index].length;
                    jumpClipFrameRate = clips[index].frameRate;
                    return;
                }
            }
        }
    }
}
