using System;
using DG.Tweening;
using UnityEngine;

namespace TowerDefense3D.Towers
{
    [DisallowMultipleComponent]
    public sealed class TowerRuntimeView : MonoBehaviour, ITowerRuntimeView
    {

        /// <summary>
        /// Stands in for a measured radius on a tower with nothing to measure - no renderers, or
        /// none enabled. Half a cell, so a ring drawn on it is still a ring rather than a point.
        /// </summary>
        private const float DefaultGroundRadiusMeters = 0.5f;

        [Tooltip("Degrees per second the tower turns to face its link. Zero snaps instantly.")]
        [SerializeField, Min(0f)] private float turnSpeedDegreesPerSecond = 540f;

        [Header("Chain state")]
        [Tooltip("The warning sign shown floating over the tower while it sits outside a working chain. Left inactive in the prefab; this view only ever switches it on and off.")]
        [SerializeField] private GameObject invalidChainWarning;

        [Tooltip("How long the sign takes to pop in. Zero shows it outright.")]
        [SerializeField, Min(0f)] private float invalidChainWarningPopDuration = 0.22f;

        [Tooltip("How big the sign is drawn, as a scale applied on top of the sprite's own size - which is its pixel size over its Pixels Per Unit, not one board cell. Given this way rather than as a plain local scale because the tower roots are authored at wildly different scales, so one local scale would come out a different size on every tower; this number does not.")]
        [SerializeField, Min(0.001f)] private float invalidChainWarningScale = 0.05f;

        [Tooltip("Gap left above the tower before the sign starts, in world units. One board cell is one unit.")]
        [SerializeField, Min(0f)] private float invalidChainWarningClearance = 0.25f;

        private TowerCombatDefinition combatDefinition;
        private Quaternion authoredLocalRotation = Quaternion.identity;
        private bool hasAuthoredRotation;
        private TowerNodeId nodeId;
        private Vector3 localPresentationAnchor;
        private Vector3 localProjectileOrigin;
        private Vector3 localGroundCentre;
        private float groundRadiusMeters = DefaultGroundRadiusMeters;
        private GameObject tierVisual;
        private Tween warningPopTween;
        private bool hasChainState;
        private bool isInValidChain;
        private Camera warningBillboardCamera;

        public event Action<ITowerRuntimeView> Destroyed;

        public TowerCombatDefinition CombatDefinition => combatDefinition;
        public TowerNodeId NodeId => nodeId;
        public bool IsConfigured => combatDefinition != null;
        public bool IsRegistered => nodeId.IsValid;
        public GameObject GameObject => gameObject;
        public Vector3 PresentationAnchor => transform.TransformPoint(localPresentationAnchor);
        public Vector3 ProjectileOrigin => transform.TransformPoint(localProjectileOrigin);
        public Vector3 FootprintOrigin => transform.position;
        public Vector3 GroundCentre => transform.TransformPoint(localGroundCentre);
        public float GroundRadiusMeters => groundRadiusMeters;

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

        /// <summary>
        /// Hangs a warning sign over the tower while it sits outside a working chain, and takes it
        /// away the moment the chain starts working.
        /// </summary>
        /// <remarks>
        /// A sign rather than a dim, because dimming says the wrong thing: a tower outside a chain
        /// is not a quieter version of a working tower, it is a tower that is doing nothing, and
        /// that deserves something the eye is drawn to rather than something it can miss.
        ///
        /// The first answer skips the pop. A tower is placed unlinked, so the sign is already due
        /// when it arrives, and animating it in would read as a second event just after the build.
        /// </remarks>
        public void SetChainValid(bool isInValidChain)
        {
            if (hasChainState && this.isInValidChain == isInValidChain)
            {
                return;
            }

            bool snap = !hasChainState;
            hasChainState = true;
            this.isInValidChain = isInValidChain;

            if (invalidChainWarning == null)
            {
                return;
            }

            warningPopTween?.Kill();
            if (isInValidChain)
            {
                invalidChainWarning.SetActive(false);
                return;
            }

            invalidChainWarning.SetActive(true);
            Vector3 restScale = ResolveWarningLocalScale();
            SeatWarningAboveTower();
            FaceWarningAtCamera();

            if (snap || invalidChainWarningPopDuration <= 0f)
            {
                invalidChainWarning.transform.localScale = restScale;
                return;
            }

            invalidChainWarning.transform.localScale = restScale * 0.4f;
            warningPopTween = invalidChainWarning.transform
                .DOScale(restScale, invalidChainWarningPopDuration)
                .SetEase(Ease.OutBack)
                .SetTarget(this);
        }

        /// <summary>
        /// Keeps the sign facing the camera, sitting above the tower, and at its intended size.
        /// </summary>
        /// <remarks>
        /// All three are re-applied while the sign is on screen rather than once when it is shown.
        /// The camera pans and zooms, so a rotation set at show time drifts. And the factory scales
        /// the tower after it instantiates it, so a size worked out at show time was measured
        /// against the wrong scale - which is what left the sign several metres across.
        ///
        /// The size is left alone while the pop is running, because that tween owns the scale until
        /// it finishes.
        /// </remarks>
        private void LateUpdate()
        {
            if (invalidChainWarning == null || !invalidChainWarning.activeSelf)
            {
                return;
            }

            SeatWarningAboveTower();
            FaceWarningAtCamera();

            bool popping = warningPopTween != null && warningPopTween.IsActive() && warningPopTween.IsPlaying();
            if (!popping)
            {
                invalidChainWarning.transform.localScale = ResolveWarningLocalScale();
            }
        }

        /// <summary>
        /// Lifts the sign clear of the top of whatever model the tower is currently wearing.
        /// </summary>
        /// <remarks>
        /// Hung off <see cref="PresentationAnchor"/> rather than measured here. That anchor is
        /// already the point just above the tower's silhouette - it is where link lines land - and
        /// it is derived once when the tower is configured or re-tiered, from bounds taken while
        /// the renderers are live. Measuring again at show time read garbage on some towers,
        /// because a renderer's bounds are not dependable the instant it is asked for them.
        ///
        /// Following the anchor also means a tier upgrade moves the sign with it, since replacing
        /// the model re-derives the anchor.
        /// </remarks>
        private void SeatWarningAboveTower()
        {
            float clearance = invalidChainWarningScale * 0.5f + invalidChainWarningClearance;
            invalidChainWarning.transform.position = PresentationAnchor + Vector3.up * clearance;
        }

        /// <summary>
        /// The local scale that draws the sign at <see cref="invalidChainWarningScale"/> however
        /// the tower itself is scaled.
        /// </summary>
        /// <remarks>
        /// The tower roots carry their own export scales, some of them very large, so the tower's
        /// scale has to be divided back out or the sign inherits it - which is what blew one sign
        /// up to dozens of metres across and parked it far above the board.
        /// </remarks>
        private Vector3 ResolveWarningLocalScale()
        {
            Vector3 towerScale = transform.lossyScale;
            return new Vector3(
                invalidChainWarningScale / Mathf.Max(0.0001f, Mathf.Abs(towerScale.x)),
                invalidChainWarningScale / Mathf.Max(0.0001f, Mathf.Abs(towerScale.y)),
                invalidChainWarningScale / Mathf.Max(0.0001f, Mathf.Abs(towerScale.z)));
        }

        private void FaceWarningAtCamera()
        {
            if (warningBillboardCamera == null)
            {
                // Null while the level is still building up or already tearing down.
                warningBillboardCamera = Camera.main;
                if (warningBillboardCamera == null)
                {
                    return;
                }
            }

            Transform view = warningBillboardCamera.transform;
            invalidChainWarning.transform.rotation =
                Quaternion.LookRotation(view.forward, view.up);
        }

        public void Despawn()
        {
            if (!Application.isPlaying)
            {
                DestroyImmediate(gameObject);
                return;
            }

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

        public void ReplaceVisual(GameObject visualPrefab)
        {
            if (visualPrefab == null)
            {
                throw new ArgumentNullException(nameof(visualPrefab));
            }

            // Read the ground contact off the model standing here rather than off the placement
            // surface: the factory seated the tower on the board when it spawned, and matching
            // whatever it rests on now keeps a taller or shorter tier from floating or sinking.
            bool wasSeated = TryMeasureVisualBounds(out Bounds seatedBounds);

            HideCurrentVisual();
            tierVisual = Instantiate(visualPrefab);
            Transform tierTransform = tierVisual.transform;

            // Each tier model was exported at its own unit scale and carries it on its prefab
            // root, so nesting it under a tower root that is itself scaled would multiply the
            // two. Dividing the tower's scale back out leaves the model at its authored size.
            Vector3 authoredScale = tierTransform.localScale;
            tierTransform.SetParent(transform, false);
            tierTransform.localPosition = Vector3.zero;
            tierTransform.localRotation = Quaternion.identity;
            tierTransform.localScale = DivideScale(authoredScale, transform.localScale);

            if (wasSeated && TryMeasureVisualBounds(out Bounds tierBounds))
            {
                transform.position += Vector3.up * (seatedBounds.min.y - tierBounds.min.y);
            }

            CalculateLocalAnchors();
        }

        private void HideCurrentVisual()
        {
            if (tierVisual != null)
            {
                // Detached before being destroyed: Destroy only takes effect at the end of the
                // frame, so the bounds measured right after this call would otherwise still find
                // the outgoing tier model hanging under the tower.
                tierVisual.transform.SetParent(null, true);
                if (Application.isPlaying)
                {
                    Destroy(tierVisual);
                }
                else
                {
                    DestroyImmediate(tierVisual);
                }

                tierVisual = null;
                return;
            }

            // The authored model is a renderer on the tower root itself, which cannot be removed
            // without taking the tower and its node binding with it. Disabling it hides it and
            // drops it out of the silhouette, which the bounds measurement already keys off.
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            Transform linkSlotsRoot = ResolveLinkSlotsRoot();

            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (linkSlotsRoot != null && renderer.transform.IsChildOf(linkSlotsRoot))
                {
                    continue;
                }

                renderer.enabled = false;
            }
        }

        private static Vector3 DivideScale(Vector3 scale, Vector3 divisor)
        {
            return new Vector3(
                divisor.x != 0f ? scale.x / divisor.x : scale.x,
                divisor.y != 0f ? scale.y / divisor.y : scale.y,
                divisor.z != 0f ? scale.z / divisor.z : scale.z);
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

        // The link-slot squares float above the tower and are not part of its silhouette.
        // Measuring them would push the presentation anchor and projectile origin up to the
        // squares' own height, moving where link lines land and where projectiles fly.
        private Transform ResolveLinkSlotsRoot()
        {
            TowerLinkSlotsView linkSlots = GetComponentInChildren<TowerLinkSlotsView>(true);
            return linkSlots != null ? linkSlots.BillboardRoot : null;
        }

        private bool TryMeasureVisualBounds(out Bounds bounds)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Bounds combinedBounds = default;

            Transform linkSlotsRoot = ResolveLinkSlotsRoot();

            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (!renderer.enabled)
                {
                    continue;
                }

                if (linkSlotsRoot != null && renderer.transform.IsChildOf(linkSlotsRoot))
                {
                    continue;
                }

                // The dead-chain sign is not part of the tower's silhouette. Counting it would
                // feed on itself - the sign is seated above these bounds, so each measurement
                // would push it higher - and it would drag the link anchor and the projectile
                // origin up with it every time a tower fell out of its chain.
                if (invalidChainWarning != null
                    && renderer.transform.IsChildOf(invalidChainWarning.transform))
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

            bounds = combinedBounds;
            return hasBounds;
        }

        private void CalculateLocalAnchors()
        {
            bool hasBounds = TryMeasureVisualBounds(out Bounds combinedBounds);

            Vector3 worldPresentationAnchor = hasBounds
                ? new Vector3(combinedBounds.center.x, combinedBounds.max.y + 0.2f, combinedBounds.center.z)
                : transform.position + Vector3.up;
            Vector3 worldProjectileOrigin = hasBounds
                ? combinedBounds.center
                : transform.position + Vector3.up;

            // The silhouette's middle, dropped to the foot of the tower rather than to the bottom
            // of its bounds: a model authored sunk into the ground would otherwise pull the ring
            // down under the board with it.
            Vector3 worldGroundCentre = hasBounds
                ? new Vector3(combinedBounds.center.x, transform.position.y, combinedBounds.center.z)
                : transform.position;

            // The wider of the two axes, so the ring clears the tower on both. Taking the smaller
            // one would tuck the ring inside the model along the other axis, which is precisely
            // how a ring ends up invisible under the thing it is meant to be pointing at.
            groundRadiusMeters = hasBounds
                ? Mathf.Max(combinedBounds.extents.x, combinedBounds.extents.z)
                : DefaultGroundRadiusMeters;

            localPresentationAnchor = transform.InverseTransformPoint(worldPresentationAnchor);
            localProjectileOrigin = transform.InverseTransformPoint(worldProjectileOrigin);
            localGroundCentre = transform.InverseTransformPoint(worldGroundCentre);
        }

        private void OnDestroy()
        {
            warningPopTween?.Kill();
            warningPopTween = null;
            Destroyed?.Invoke(this);
        }
    }
}
