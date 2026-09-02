using System;
using NUnit.Framework;
using TowerDefense3D.Mobile;

namespace TowerDefense3D.Mobile.Tests.EditMode
{
    public sealed class FrameRateSamplerTests
    {
        [Test]
        public void TryTakeAverage_SaysNothingUntilTheWindowHasPassed()
        {
            var sampler = new FrameRateSampler(0.25f);
            for (int frame = 0; frame < 10; frame++)
            {
                sampler.Add(1f / 60f);
            }

            Assert.That(
                sampler.TryTakeAverage(out float framesPerSecond),
                Is.False,
                "Ten frames at sixty is a sixth of a second, short of the window.");
            Assert.That(framesPerSecond, Is.Zero);
        }

        [Test]
        public void TryTakeAverage_ReportsTheRateOverTheWindow()
        {
            var sampler = new FrameRateSampler(0.25f);
            for (int frame = 0; frame < 15; frame++)
            {
                sampler.Add(1f / 60f);
            }

            Assert.That(sampler.TryTakeAverage(out float framesPerSecond), Is.True);
            Assert.That(framesPerSecond, Is.EqualTo(60f).Within(0.01f));
        }

        /// <summary>
        /// The average is of the window, not of everything since the game started, so a stretch of
        /// slow frames has to stop dragging the number down once it is over.
        /// </summary>
        [Test]
        public void TryTakeAverage_StartsTheNextWindowFresh()
        {
            var sampler = new FrameRateSampler(0.25f);
            for (int frame = 0; frame < 8; frame++)
            {
                sampler.Add(1f / 30f);
            }

            Assert.That(sampler.TryTakeAverage(out float slow), Is.True);
            Assert.That(slow, Is.EqualTo(30f).Within(0.01f));

            for (int frame = 0; frame < 15; frame++)
            {
                sampler.Add(1f / 60f);
            }

            Assert.That(sampler.TryTakeAverage(out float recovered), Is.True);
            Assert.That(
                recovered,
                Is.EqualTo(60f).Within(0.01f),
                "The slow window must not still be counted in the next one.");
        }

        [Test]
        public void Add_DropsFramesThatReportNoUsableTime()
        {
            var sampler = new FrameRateSampler(0.1f);
            sampler.Add(0f);
            sampler.Add(-1f / 60f);
            sampler.Add(float.NaN);
            sampler.Add(float.PositiveInfinity);

            Assert.That(
                sampler.TryTakeAverage(out _),
                Is.False,
                "None of those frames should have advanced the window.");

            for (int frame = 0; frame < 6; frame++)
            {
                sampler.Add(1f / 60f);
            }

            Assert.That(sampler.TryTakeAverage(out float framesPerSecond), Is.True);
            Assert.That(
                framesPerSecond,
                Is.EqualTo(60f).Within(0.01f),
                "The dropped frames must not be counted in the average either.");
        }

        [Test]
        public void Constructor_RejectsAWindowWithNoLength()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FrameRateSampler(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FrameRateSampler(-0.25f));
        }

        [Test]
        public void Resolve_GradesTheRateAgainstTheTarget()
        {
            const int target = FramePacingSystem.TargetFrameRate;

            Assert.That(FrameRateHealthScale.Resolve(target, target), Is.EqualTo(FrameRateHealth.Good));
            Assert.That(FrameRateHealthScale.Resolve(58f, target), Is.EqualTo(FrameRateHealth.Good));
            Assert.That(FrameRateHealthScale.Resolve(45f, target), Is.EqualTo(FrameRateHealth.Fair));
            Assert.That(FrameRateHealthScale.Resolve(30f, target), Is.EqualTo(FrameRateHealth.Poor));
            Assert.That(FrameRateHealthScale.Resolve(0f, target), Is.EqualTo(FrameRateHealth.Poor));
        }

        /// <summary>Nothing to fall short of means nothing to grade, so it reads as fine.</summary>
        [Test]
        public void Resolve_TreatsAnUncappedFrameRateAsGood()
        {
            Assert.That(FrameRateHealthScale.Resolve(12f, 0), Is.EqualTo(FrameRateHealth.Good));
        }
    }
}
