using TowerDefense3D.Waves;
using UnityEngine;

namespace TowerDefense3D.Enemies
{
    /// <summary>
    /// Drag this onto the spot where the boss should wait. That is the whole of it.
    /// </summary>
    /// <remarks>
    /// The boss appears wherever this sits, snapped onto the nearest point of the road it is
    /// standing on. Snapping is what lets it be placed roughly: the marker only has to be near the
    /// road, not exactly on its centre line, and the boss still ends up somewhere it can stand and
    /// somewhere its summons can walk out from.
    ///
    /// Nothing has to be pressed after moving it. The distance the spawn works in is measured from
    /// this position each time a wave starts, so the marker is the setting rather than a source
    /// for one.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class StandingBossAnchorView : MonoBehaviour, IStandingBossAnchor
    {
        [Tooltip("Radius of the marker drawn in the Scene view. Display only.")]
        [SerializeField, Min(0.1f)] private float gizmoRadius = 0.6f;

        public bool HasAnchor => isActiveAndEnabled;

        public Vector3 WorldPosition => transform.position;

        public float FacingYawDegrees => transform.eulerAngles.y;

        /// <summary>
        /// Drawn always rather than only when selected, so the spot can be seen while dragging
        /// something else - which is exactly when its position matters.
        /// </summary>
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.35f, 0.15f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, gizmoRadius);
            Gizmos.DrawLine(
                transform.position,
                transform.position + (Vector3.up * (gizmoRadius * 3f)));

            // The way the boss will look, so the marker's rotation is visible while turning it.
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.95f);
            Gizmos.DrawRay(transform.position, transform.forward * (gizmoRadius * 4f));
        }
    }
}
