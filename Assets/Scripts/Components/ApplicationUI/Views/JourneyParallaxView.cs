using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Slides the backdrop layers as the journey trail is dragged, each at its own speed, so the
    /// map reads as having depth rather than as a flat picture behind a moving row of nodes.
    ///
    /// The layers are its own children and it takes them in drawing order: the first child is the
    /// furthest away and moves least, the last is nearest and moves most. Nothing has to be wired
    /// up, so adding a layer to the hierarchy is all it takes to put it in the ramp.
    ///
    /// Two kinds of child are left alone: anything named Fog, which is a wash meant to sit still,
    /// and the scroll view itself, which is the trail this parallaxes against.
    ///
    /// A layer can also sit in FRONT of the trail. Being nearer than the trail, a foreground band
    /// has to slide further than the trail rather than less, so it is taken off the depth ramp
    /// and given <see cref="foregroundFactor"/> instead. Adding one leaves every background
    /// layer at the speed it already had.
    ///
    /// A layer carrying a <see cref="CloudDriftView"/> also wanders on its own while nothing is
    /// being dragged. That offset is added here rather than written by the drift itself, because
    /// this component rewrites the layer's position outright and would otherwise erase it.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class JourneyParallaxView : MonoBehaviour
    {
        /// <summary>Children whose name starts with this are washes, and washes do not move.</summary>
        private const string StillLayerPrefix = "Fog";

        [SerializeField] private ScrollRect scroll;

        [Tooltip("Share of the trail's movement taken by the furthest layer.")]
        [SerializeField] private float slowestFactor = 0.08f;

        [Tooltip("Share of the trail's movement taken by the nearest layer.")]
        [SerializeField] private float fastestFactor = 0.5f;

        [Tooltip("Share of the trail's movement taken by a foreground layer. Above one so it outruns the trail, which is what reads as being nearer than the nodes.")]
        [SerializeField] private float foregroundFactor = 1.3f;

        private readonly List<RectTransform> layers = new();
        private readonly List<Vector2> homePositions = new();
        private readonly List<float> factors = new();
        private readonly List<bool> foregrounds = new();
        private readonly List<CloudDriftView> drifts = new();
        private bool hasDrift;
        private bool hasApplied;
        private float appliedTravel;

        private void OnEnable()
        {
            Collect();
            Apply(ReadTravel());
        }

        private void OnDisable()
        {
            SendLayersHome();
            ReleaseDrifts();
        }

        private void LateUpdate()
        {
            float travel = ReadTravel();

            // A drifting layer moves on its own clock, so a still trail is no longer proof that
            // nothing has changed. Without a drift the early-out stands and an idle map costs
            // nothing.
            if (hasApplied && Mathf.Approximately(travel, appliedTravel) && !hasDrift)
            {
                return;
            }

            Apply(travel);
        }

        /// <summary>
        /// Takes the layers in drawing order and works out how fast each one goes. Positions are
        /// remembered here rather than on the first slide, because the offset is measured from where
        /// the layer was authored and that has to survive the screen being shown twice.
        /// </summary>
        private void Collect()
        {
            SendLayersHome();
            ReleaseDrifts();
            layers.Clear();
            homePositions.Clear();
            factors.Clear();
            foregrounds.Clear();
            drifts.Clear();
            hasDrift = false;

            if (scroll == null)
            {
                scroll = GetComponentInChildren<ScrollRect>(true);
            }

            var owner = (RectTransform)transform;
            for (int index = 0; index < owner.childCount; index++)
            {
                if (owner.GetChild(index) is not RectTransform child)
                {
                    continue;
                }

                if (child.name.StartsWith(StillLayerPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Found by component rather than by name: the trail is what everything else moves
                // against, and renaming it must not quietly enlist it as a layer.
                if (child.GetComponent<ScrollRect>() != null)
                {
                    continue;
                }

                layers.Add(child);

                // Claimed before the position is read: taking the layer over puts it back at its
                // authored spot, and reading first would take a drifted position for home and let
                // the layer creep a little further away every time the screen is shown.
                CloudDriftView drift = child.GetComponent<CloudDriftView>();
                if (drift != null)
                {
                    drift.SetDrivenExternally(true);
                    hasDrift = true;
                }

                drifts.Add(drift);
                homePositions.Add(child.anchoredPosition);

                // A foreground layer is one that draws above the chrome, and the only way a
                // child of the map can do that is to carry its own sorting canvas. Read off that
                // component rather than off a name, so the layer that looks nearest and the layer
                // that moves nearest cannot drift apart.
                foregrounds.Add(
                    child.TryGetComponent(out Canvas sorting) && sorting.overrideSorting);
            }

            RampFactors();

            hasApplied = false;
        }

        /// <summary>
        /// Spreads the depth ramp across the background layers only, and hands every foreground
        /// layer the one factor that puts it in front of the trail.
        /// </summary>
        /// <remarks>
        /// Counting the backgrounds on their own is what keeps a foreground band from being a
        /// breaking change: were the ramp spread over all the layers, hanging one in front would
        /// quietly slow every band behind it and the tuning would have to be redone.
        /// </remarks>
        private void RampFactors()
        {
            int backgroundCount = 0;
            for (int index = 0; index < foregrounds.Count; index++)
            {
                if (!foregrounds[index])
                {
                    backgroundCount++;
                }
            }

            int backgroundIndex = 0;
            for (int index = 0; index < layers.Count; index++)
            {
                if (foregrounds[index])
                {
                    factors.Add(foregroundFactor);
                    continue;
                }

                factors.Add(ParallaxDepthRamp.ResolveFactor(
                    backgroundIndex,
                    backgroundCount,
                    slowestFactor,
                    fastestFactor));
                backgroundIndex++;
            }
        }

        /// <summary>
        /// How far the trail has been dragged, in the same units the layers are positioned in. The
        /// content's own offset is used rather than a normalised position so a layer on a factor of
        /// one would track the trail exactly.
        /// </summary>
        private float ReadTravel()
        {
            return scroll != null && scroll.content != null
                ? scroll.content.anchoredPosition.x
                : 0f;
        }

        private void Apply(float travel)
        {
            for (int index = 0; index < layers.Count; index++)
            {
                CloudDriftView drift = drifts[index];
                Vector2 wander = drift != null && drift.isActiveAndEnabled ? drift.Offset : Vector2.zero;

                layers[index].anchoredPosition =
                    homePositions[index] + new Vector2(travel * factors[index], 0f) + wander;
            }

            appliedTravel = travel;
            hasApplied = true;
        }

        /// <summary>
        /// Hands every claimed layer back to its own drift, so a cloud left in the hierarchy after
        /// this component is switched off keeps moving instead of freezing.
        /// </summary>
        private void ReleaseDrifts()
        {
            for (int index = 0; index < drifts.Count; index++)
            {
                if (drifts[index] != null)
                {
                    drifts[index].SetDrivenExternally(false);
                }
            }
        }

        private void SendLayersHome()
        {
            for (int index = 0; index < layers.Count && index < homePositions.Count; index++)
            {
                if (layers[index] != null)
                {
                    layers[index].anchoredPosition = homePositions[index];
                }
            }

            hasApplied = false;
        }
    }
}
