using System;
using NUnit.Framework;
using TowerDefense3D.Core;

namespace TowerDefense3D.Simulation.Tests.EditMode
{
    public sealed class FixedStepClockTests
    {
        [Test]
        public void Advance_ExecutesDeterministicStepsAndKeepsRemainder()
        {
            var clock = new FixedStepClock(0.05f);
            int executed = 0;

            int firstFrameSteps = clock.Advance(0.12f, () => executed++);
            int secondFrameSteps = clock.Advance(0.03f, () => executed++);

            Assert.That(firstFrameSteps, Is.EqualTo(2));
            Assert.That(secondFrameSteps, Is.EqualTo(1));
            Assert.That(executed, Is.EqualTo(3));
            Assert.That(clock.InterpolationAlpha, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void Advance_LongFrame_ExecutesEveryCrossedStep()
        {
            var clock = new FixedStepClock(0.05f);
            int executed = 0;

            int executedSteps = clock.Advance(0.21f, () => executed++);

            Assert.That(executedSteps, Is.EqualTo(4));
            Assert.That(executed, Is.EqualTo(4));
            Assert.That(clock.AccumulatedSeconds, Is.EqualTo(0.01d).Within(0.000001d));
        }

        [Test]
        public void InterpolationAlpha_UsesRemainingStepFraction()
        {
            var clock = new FixedStepClock(0.05f);

            clock.Advance(0.12f, () => { });

            Assert.That(clock.InterpolationAlpha, Is.EqualTo(0.4f).Within(0.00001f));
        }

        [Test]
        public void Reset_ClearsAccumulatedTime()
        {
            var clock = new FixedStepClock(0.05f);
            clock.Advance(0.03f, () => { });

            clock.Reset();

            Assert.That(clock.AccumulatedSeconds, Is.Zero);
            Assert.That(clock.InterpolationAlpha, Is.Zero);
        }

        [TestCase(-0.01f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void Advance_InvalidDeltaTime_Throws(float deltaTimeSeconds)
        {
            var clock = new FixedStepClock(0.05f);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => clock.Advance(deltaTimeSeconds, () => { }));
        }

        [Test]
        public void Advance_NullStep_Throws()
        {
            var clock = new FixedStepClock(0.05f);

            Assert.Throws<ArgumentNullException>(() => clock.Advance(0.05f, null));
        }
    }
}
