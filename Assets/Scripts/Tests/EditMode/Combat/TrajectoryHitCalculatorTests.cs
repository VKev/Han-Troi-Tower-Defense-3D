using NUnit.Framework;
using UnityEngine;

namespace TowerDefense3D.Enemies.Tests.EditMode
{
    public sealed class TrajectoryHitCalculatorTests
    {
        [Test]
        public void IntersectsXZ_DetectsCrossingBetweenTicks()
        {
            bool hit = TrajectoryHitCalculator.IntersectsXZ(
                new Vector3(-1f, 10f, 0f),
                new Vector3(1f, 10f, 0f),
                new Vector3(0f, -10f, -1f),
                new Vector3(0f, -10f, 1f),
                0.1f);

            Assert.That(hit, Is.True);
        }

        [Test]
        public void IntersectsXZ_IgnoresHeightButRejectsSeparatedTrajectories()
        {
            bool heightOnlyDifference = TrajectoryHitCalculator.IntersectsXZ(
                Vector3.zero,
                Vector3.right,
                Vector3.up * 100f,
                Vector3.right + Vector3.up * 100f,
                0.1f);
            bool separated = TrajectoryHitCalculator.IntersectsXZ(
                Vector3.zero,
                Vector3.right,
                Vector3.forward * 2f,
                Vector3.right + Vector3.forward * 2f,
                0.1f);

            Assert.That(heightOnlyDifference, Is.True);
            Assert.That(separated, Is.False);
        }
    }
}
