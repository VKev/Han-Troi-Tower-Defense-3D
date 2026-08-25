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

        void Configure(TowerCombatDefinition definition);
        void BindNode(TowerNodeId nodeId);
        void ClearNodeBinding();
    }
}
