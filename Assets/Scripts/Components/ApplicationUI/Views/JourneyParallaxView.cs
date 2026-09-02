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

        private readonly List<RectTransform> layers = new();
        private readonly List<Vector2> homePositions = new();
        private readonly List<float> factors = new();
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
        }

        private void LateUpdate()
        {
            float travel = ReadTravel();
            if (hasApplied && Mathf.Approximately(travel, appliedTravel))
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
            layers.Clear();
            homePositions.Clear();
            factors.Clear();

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
                homePositions.Add(child.anchoredPosition);
            }

            for (int index = 0; index < layers.Count; index++)
            {
                factors.Add(ParallaxDepthRamp.ResolveFactor(
                    index,
                    layers.Count,
                    slowestFactor,
                    fastestFactor));
            }

            hasApplied = false;
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
                layers[index].anchoredPosition =
                    homePositions[index] + new Vector2(travel * factors[index], 0f);
            }

            appliedTravel = travel;
            hasApplied = true;
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
