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
        Vector3 PresentationAnchor { get; }
        Vector3 ProjectileOrigin { get; }

        /// <summary>
        /// Turns the tower to look at a world position, flattened to the ground plane.
        /// </summary>
        void FaceTowards(Vector3 worldPosition);

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
