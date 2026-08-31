using System;
using UnityEngine;

namespace TowerDefense3D.Towers
{
    [DisallowMultipleComponent]
    public sealed class TowerRuntimeView : MonoBehaviour, ITowerRuntimeView
    {
        [Tooltip("Degrees per second the tower turns to face its link. Zero snaps instantly.")]
        [SerializeField, Min(0f)] private float turnSpeedDegreesPerSecond = 540f;

        private TowerCombatDefinition combatDefinition;
        private Quaternion authoredLocalRotation = Quaternion.identity;
        private bool hasAuthoredRotation;
        private TowerNodeId nodeId;
        private Vector3 localPresentationAnchor;
        private Vector3 localProjectileOrigin;

        public event Action<ITowerRuntimeView> Destroyed;

        public TowerCombatDefinition CombatDefinition => combatDefinition;
        public TowerNodeId NodeId => nodeId;
        public bool IsConfigured => combatDefinition != null;
        public bool IsRegistered => nodeId.IsValid;
        public GameObject GameObject => gameObject;
        public Vector3 PresentationAnchor => transform.TransformPoint(localPresentationAnchor);
        public Vector3 ProjectileOrigin => transform.TransformPoint(localProjectileOrigin);
        public Vector3 FootprintOrigin => transform.position;

        public void SetFootprintOrigin(Vector3 worldPosition)
        {
            transform.position = worldPosition;
        }

        /// <summary>
        /// Spins the tower about the world up axis only, layered on top of however the prefab was
        /// authored. Assigning a look rotation outright would discard that authored orientation
        /// and lay a tower on its side if its model is not built facing +Z upright.
        ///
        /// Aimed from the transform rather than from PresentationAnchor, because the anchor is
        /// derived from this transform and steering by it would chase its own output.
        /// </summary>
        public void FaceTowards(Vector3 worldPosition)
        {
            EnsureAuthoredRotation();
            Vector3 direction = worldPosition - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            float yawDegrees = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            Quaternion target = Quaternion.AngleAxis(yawDegrees, Vector3.up) * authoredLocalRotation;
            transform.localRotation = turnSpeedDegreesPerSecond <= 0f
                ? target
                : Quaternion.RotateTowards(
                    transform.localRotation,
                    target,
                    turnSpeedDegreesPerSecond * Time.deltaTime);
        }

        private void EnsureAuthoredRotation()
        {
            if (hasAuthoredRotation)
            {
                return;
            }

            authoredLocalRotation = transform.localRotation;
            hasAuthoredRotation = true;
        }

        public void Despawn()
        {
            Destroy(gameObject);
        }

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
            CalculateLocalAnchors();
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

        private void CalculateLocalAnchors()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Bounds combinedBounds = default;

            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (!renderer.enabled)
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

            Vector3 worldPresentationAnchor = hasBounds
                ? new Vector3(combinedBounds.center.x, combinedBounds.max.y + 0.2f, combinedBounds.center.z)
                : transform.position + Vector3.up;
            Vector3 worldProjectileOrigin = hasBounds
                ? combinedBounds.center
                : transform.position + Vector3.up;
            localPresentationAnchor = transform.InverseTransformPoint(worldPresentationAnchor);
            localProjectileOrigin = transform.InverseTransformPoint(worldProjectileOrigin);
        }

        private void OnDestroy()
        {
            Destroyed?.Invoke(this);
        }
    }
}
