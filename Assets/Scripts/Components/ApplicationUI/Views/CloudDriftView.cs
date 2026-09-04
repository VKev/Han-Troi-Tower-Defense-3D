using UnityEngine;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Drifts one cloud sideways and back again, so the sky keeps moving while the player is not
    /// dragging the trail.
    ///
    /// The cloud sets off in the <see cref="direction"/> it is given, goes <see cref="distance"/>,
    /// rests, comes home, rests again, then repeats. It slows into each turn and waits there
    /// rather than reversing on the spot, because wind does not change direction all at once and
    /// a cloud that does reads as a mechanism. It never crosses to the other side of where it was
    /// authored, so a cloud placed to sit clear of a level node stays clear of it.
    ///
    /// Where a <see cref="JourneyParallaxView"/> already owns the cloud - the backdrop bands are
    /// its layers and it writes their position every frame - the parallax takes the offset from
    /// here and adds it to the slide it was going to write anyway. Two components writing one
    /// anchoredPosition would just mean whichever ran last erased the other, and which one that is
    /// is not something the authoring should have to know.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class CloudDriftView : MonoBehaviour
    {
        /// <summary>Which side of its authored spot the cloud drifts to.</summary>
        public enum DriftDirection
        {
            Left = -1,
            Right = 1
        }

        [Tooltip("Which way the cloud sets off before it turns back.")]
        [SerializeField] private DriftDirection direction = DriftDirection.Right;

        [Tooltip("Units per second averaged over one leg. A few units a second reads as weather; much more reads as traffic.")]
        [SerializeField] private float speed = 6f;

        [Tooltip("How far the cloud goes before it turns around, measured from where it was authored.")]
        [SerializeField] private float distance = 120f;

        [Tooltip("How long the cloud rests at each end before it sets off the other way. This is what keeps the turn from reading as a bounce.")]
        [SerializeField] private float holdSeconds = 2f;

        [Tooltip("Where in its round trip the cloud starts. Give neighbouring clouds different values so they do not drift in lockstep.")]
        [Range(0f, 1f)]
        [SerializeField] private float phase;

        private RectTransform rect;
        private Vector2 homePosition;
        private float elapsedSeconds;
        private bool drivenExternally;

        /// <summary>
        /// How far the cloud currently sits from where it was authored. Read by whatever owns the
        /// cloud's position when that is not this component.
        /// </summary>
        public Vector2 Offset =>
            new Vector2(
                CloudDrift.ResolveOffset(elapsedSeconds, speed, distance, phase, holdSeconds)
                * (int)direction,
                0f);

        private void Awake()
        {
            rect = (RectTransform)transform;

            // Taken once, before anything has had a chance to move the cloud. Re-reading it later
            // would risk taking a drifted position for the authored one and letting the cloud walk
            // away from where it was placed.
            homePosition = rect.anchoredPosition;
        }

        private void OnEnable()
        {
            // Every showing starts the round trip from home, so a cloud is never first seen
            // mid-drift at whatever offset the last showing happened to end on.
            elapsedSeconds = 0f;
        }

        private void OnDisable()
        {
            if (!drivenExternally && rect != null)
            {
                rect.anchoredPosition = homePosition;
            }
        }

        private void LateUpdate()
        {
            // Kept running either way: when an owner is applying the offset instead, it is still
            // this clock the offset is read off.
            elapsedSeconds += Time.deltaTime;

            if (drivenExternally)
            {
                return;
            }

            rect.anchoredPosition = homePosition + Offset;
        }

        /// <summary>
        /// Hands the cloud's position over to a caller that writes it itself, or takes it back.
        /// The cloud is put back at its authored spot on the way over, so the new owner reads the
        /// position that was authored rather than whatever offset this component last left behind.
        /// </summary>
        public void SetDrivenExternally(bool driven)
        {
            if (driven && !drivenExternally && rect != null)
            {
                rect.anchoredPosition = homePosition;
            }

            drivenExternally = driven;
        }
    }
}
