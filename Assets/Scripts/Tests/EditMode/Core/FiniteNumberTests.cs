using NUnit.Framework;

namespace TowerDefense3D.Core.Tests.EditMode
{
    public sealed class FiniteNumberTests
    {
        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.MaxValue)]
        public void IsFinite_FiniteValue_ReturnsTrue(float value)
        {
            Assert.That(FiniteNumber.IsFinite(value), Is.True);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void IsFinite_NonFiniteValue_ReturnsFalse(float value)
        {
            Assert.That(FiniteNumber.IsFinite(value), Is.False);
        }
    }
}
