using System;
using UnityEngine;

namespace TowerDefense3D.Towers
{
    [DisallowMultipleComponent]
    public sealed class TowerRuntimeView : MonoBehaviour
    {
        private TowerCombatDefinition combatDefinition;
        private TowerNodeId nodeId;
        private Vector3 localPresentationAnchor;

        public event Action<TowerRuntimeView> Destroyed;

        public TowerCombatDefinition CombatDefinition => combatDefinition;
        public TowerNodeId NodeId => nodeId;
        public bool IsConfigured => combatDefinition != null;
        public bool IsRegistered => nodeId.IsValid;
        public Vector3 PresentationAnchor => transform.TransformPoint(localPresentationAnchor);

        public void Configure(TowerCombatDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (combatDefinition != null && combatDefinition != definition)
            {
                throw new InvalidOperationException("TowerRuntimeView is already configured with another definition.");
            }

            combatDefinition = definition;
            localPresentationAnchor = CalculateLocalPresentationAnchor();
        }

        public void BindNode(TowerNodeId registeredNodeId)
        {
            if (!IsConfigured)
            {
                throw new InvalidOperationException("TowerRuntimeView must be configured before node binding.");
            }

            if (!registeredNodeId.IsValid)
            {
                throw new ArgumentException("Registered tower node ID must be valid.", nameof(registeredNodeId));
            }

            if (nodeId.IsValid && !nodeId.Equals(registeredNodeId))
            {
                throw new InvalidOperationException("TowerRuntimeView is already bound to another node.");
            }

            nodeId = registeredNodeId;
        }

        public void ClearNodeBinding()
        {
            nodeId = default;
        }

        private Vector3 CalculateLocalPresentationAnchor()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Bounds combinedBounds = default;

            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    combinedBounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(renderer.bounds);
                }
            }

            Vector3 worldAnchor = hasBounds
                ? new Vector3(combinedBounds.center.x, combinedBounds.max.y + 0.2f, combinedBounds.center.z)
                : transform.position + Vector3.up;
            return transform.InverseTransformPoint(worldAnchor);
        }

        private void OnDestroy()
        {
            Destroyed?.Invoke(this);
        }
    }
}
