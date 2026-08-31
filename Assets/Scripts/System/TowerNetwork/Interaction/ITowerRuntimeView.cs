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
