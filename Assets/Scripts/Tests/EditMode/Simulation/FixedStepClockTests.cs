using NUnit.Framework;
using TowerDefense3D.Simulation;

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
    }
}
