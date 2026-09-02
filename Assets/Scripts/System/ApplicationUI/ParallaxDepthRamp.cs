using System;
using UnityEngine;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// How fast each backdrop layer slides against the journey trail.
    ///
    /// Depth is read off the drawing order: the layer drawn first is the one furthest away, so it
    /// moves least, and each layer in front of it moves a little more until the nearest one. That
    /// is all parallax is - the far hills barely shift while the near clouds sweep past - and
    /// spreading the speeds evenly across the layers is what makes the depth read.
    /// </summary>
    public static class ParallaxDepthRamp
    {
        /// <summary>
        /// The share of the trail's own movement that the layer at <paramref name="index"/> takes.
        /// Index zero is the furthest layer and gets <paramref name="slowestFactor"/>; the last
        /// index is the nearest and gets <paramref name="fastestFactor"/>.
        /// </summary>
        public static float ResolveFactor(
            int index,
            int layerCount,
            float slowestFactor,
            float fastestFactor)
        {
            if (layerCount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(layerCount),
                    layerCount,
                    "A parallax ramp needs at least one layer.");
            }

            if (index < 0 || index >= layerCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(index),
                    index,
                    "Layer index is outside the ramp of " + layerCount + " layers.");
            }

            // One layer has no ramp to sit on, so it takes the far end: a lone backdrop reads as
            // distant, and having it race the trail would look like a mistake.
            if (layerCount == 1)
            {
                return slowestFactor;
            }

            return Mathf.Lerp(slowestFactor, fastestFactor, index / (float)(layerCount - 1));
        }
    }
}
