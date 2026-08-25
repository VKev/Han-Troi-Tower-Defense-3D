using NUnit.Framework;
using UnityEngine;

namespace TowerDefense3D.Enemies.Tests.EditMode
{
    public sealed class TrajectoryHitCalculatorTests
    {
        [Test]
        public void TryFindFirstIntersectionTimeXZ_ReturnsEarliestContactTime()
        {
            bool hit = TrajectoryHitCalculator.TryFindFirstIntersectionTimeXZ(
                new Vector3(-1f, 10f, 0f),
                Vector3.right * 2f,
                new Vector3(0f, -10f, -1f),
                Vector3.forward * 2f,
                1f,
                0.1f,
                out float intersectionTime);

            Assert.That(hit, Is.True);
            Assert.That(intersectionTime, Is.EqualTo(0.46464f).Within(0.0001f));
        }

        [Test]
        public void TryFindFirstIntersectionTimeXZ_RejectsContactAfterInterval()
        {
            bool hit = TrajectoryHitCalculator.TryFindFirstIntersectionTimeXZ(
                Vector3.zero,
                Vector3.right,
                Vector3.right * 2f,
                Vector3.zero,
                1f,
                0.1f,
                out _);

            Assert.That(hit, Is.False);
        }
    }
}
