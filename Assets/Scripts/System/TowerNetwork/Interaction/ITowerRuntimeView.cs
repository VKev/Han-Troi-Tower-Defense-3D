using System;
using UnityEngine;

namespace TowerDefense3D.Towers
{
    /// <summary>
    /// Unity-facing tower instance boundary consumed by the tower-network systems.
    /// </summary>
    public interface ITowerRuntimeView
    {
        event Action<ITowerRuntimeView> Destroyed;

        TowerCombatDefinition CombatDefinition { get; }
        TowerNodeId NodeId { get; }
        bool IsConfigured { get; }
        bool IsRegistered { get; }
        GameObject GameObject { get; }
        Vector3 PresentationAnchor { get; }
        Vector3 ProjectileOrigin { get; }

        /// <summary>
        /// Where the tower's footprint meets the board - the point grid placement positions a
        /// tower instance at. Reading and writing it lets an authored tower be snapped onto the
        /// same grid a dragged one lands on.
        /// </summary>
        Vector3 FootprintOrigin { get; }

        /// <summary>
        /// Where the tower stands on the board: the centre of its silhouette, dropped to the
        /// height its footprint meets the ground at.
        /// </summary>
        /// <remarks>
        /// Not the same point as <see cref="FootprintOrigin"/>. A tower's pivot is wherever its
        /// prefab was authored with one, and several of them sit on the model's edge rather than
        /// under its middle - so anything drawn concentric with the tower has to be drawn here or
        /// it comes out visibly off to one side.
        ///
        /// It is also the point the network measures link range from, since a tower registers at
        /// its <see cref="ProjectileOrigin"/>. A reach ring drawn anywhere else would be a circle
        /// that disagrees with the rule it is drawn to explain.
        /// </remarks>
        Vector3 GroundCentre { get; }

        /// <summary>
        /// How far the tower's silhouette reaches from <see cref="GroundCentre"/> across the
        /// board, in metres. What a ring has to clear to be seen rather than swallowed.
        /// </summary>
        float GroundRadiusMeters { get; }

        /// <summary>
        /// Turns the tower to look at a world position, flattened to the ground plane.
        /// </summary>
        void FaceTowards(Vector3 worldPosition);

        /// <summary>
        /// Moves the tower so its footprint meets the board at <paramref name="worldPosition"/>.
        /// </summary>
        void SetFootprintOrigin(Vector3 worldPosition);

        /// <summary>
        /// Removes the tower from the scene. Raises <see cref="Destroyed"/> like any other
        /// teardown, so the network unregisters the node through its usual path.
        /// </summary>
        void Despawn();

        void Configure(TowerCombatDefinition definition);
        void BindNode(TowerNodeId nodeId);
        void ClearNodeBinding();
    }
}
