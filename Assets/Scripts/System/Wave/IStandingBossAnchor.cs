using UnityEngine;

namespace TowerDefense3D.Waves
{
    /// <summary>
    /// The spot in the level where the standing boss waits, placed by hand in the scene.
    /// </summary>
    /// <remarks>
    /// The spot belongs in the scene rather than in the wave schedule because it is a place on a
    /// map, and a place on a map is chosen by looking at it. Everything else about the boss - which
    /// waves it casts on, how often, and what walks out - is numbers, and numbers belong in the
    /// schedule where they can be tuned.
    ///
    /// What the spawn actually needs is a distance along the road, and that is derived from this
    /// position when the wave starts. Deriving it rather than storing it means the marker can be
    /// dragged and the boss simply appears where it was dragged to, with no step in between to
    /// forget.
    /// </remarks>
    public interface IStandingBossAnchor
    {
        /// <summary>Whether this level places the boss by hand.</summary>
        bool HasAnchor { get; }

        /// <summary>Where the marker sits. Projected onto the road before it is used.</summary>
        Vector3 WorldPosition { get; }

        /// <summary>
        /// Which way the boss looks, as a yaw in degrees. Taken from the marker's own rotation, so
        /// turning the marker turns the boss.
        /// </summary>
        /// <remarks>
        /// It has to be authored because it cannot be derived: every other enemy faces the way it
        /// is walking, and this one never walks.
        /// </remarks>
        float FacingYawDegrees { get; }
    }
}
