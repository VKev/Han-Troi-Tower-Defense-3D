using System;
using NUnit.Framework;

namespace TowerDefense3D.GameFlow.Tests.EditMode
{
    public sealed class ParallaxDepthRampTests
    {
        private const float Slowest = 0.08f;
        private const float Fastest = 0.5f;

        /// <summary>
        /// The point of the ramp: the layer drawn first barely moves, the one drawn last moves most,
        /// and every layer between them is faster than the one behind it. Without that order the
        /// layers read as one flat picture however fast they slide.
        /// </summary>
        [Test]
        public void ResolveFactor_SpeedsUpWithEveryLayerDrawnInFront()
        {
            const int layerCount = 6;
            float previous = float.NegativeInfinity;
            for (int index = 0; index < layerCount; index++)
            {
                float factor = ParallaxDepthRamp.ResolveFactor(index, layerCount, Slowest, Fastest);
                Assert.That(
                    factor,
                    Is.GreaterThan(previous),
                    "Layer " + index + " must move faster than the one behind it.");
                previous = factor;
            }

            Assert.That(
                ParallaxDepthRamp.ResolveFactor(0, layerCount, Slowest, Fastest),
                Is.EqualTo(Slowest).Within(0.0001f));
            Assert.That(
                ParallaxDepthRamp.ResolveFactor(layerCount - 1, layerCount, Slowest, Fastest),
                Is.EqualTo(Fastest).Within(0.0001f));
        }

        [Test]
        public void ResolveFactor_SpreadsTheLayersEvenly()
        {
            Assert.That(
                ParallaxDepthRamp.ResolveFactor(1, 3, 0f, 1f),
                Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(
                ParallaxDepthRamp.ResolveFactor(1, 5, 0f, 1f),
                Is.EqualTo(0.25f).Within(0.0001f));
        }

        /// <summary>A lone backdrop reads as distant, so it takes the slow end rather than racing.</summary>
        [Test]
        public void ResolveFactor_GivesASingleLayerTheSlowEnd()
        {
            Assert.That(
                ParallaxDepthRamp.ResolveFactor(0, 1, Slowest, Fastest),
                Is.EqualTo(Slowest).Within(0.0001f));
        }

        [Test]
        public void ResolveFactor_RejectsAnIndexOutsideTheRamp()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => ParallaxDepthRamp.ResolveFactor(3, 3, Slowest, Fastest));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => ParallaxDepthRamp.ResolveFactor(-1, 3, Slowest, Fastest));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => ParallaxDepthRamp.ResolveFactor(0, 0, Slowest, Fastest));
        }
    }
}
