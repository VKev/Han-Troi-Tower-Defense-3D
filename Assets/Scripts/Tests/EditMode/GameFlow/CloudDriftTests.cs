using NUnit.Framework;

namespace TowerDefense3D.GameFlow.Tests.EditMode
{
    /// <summary>
    /// Two things make a drifting cloud read as weather rather than as a mechanism.
    ///
    /// It has to keep coming back - drifting is only worth having if the sky still looks the way
    /// it was authored a minute later, and the ways that fails are a cloud that sails off the
    /// screen and a cloud that swings past its authored spot onto ground it was placed to avoid.
    ///
    /// And it has to turn gently. Wind slows, hangs, and picks up the other way; a cloud that
    /// arrives at its far end still at full speed and immediately heads back is the one thing an
    /// eye catches in an otherwise still backdrop.
    /// </summary>
    public sealed class CloudDriftTests
    {
        private const float Speed = 10f;
        private const float Distance = 100f;
        private const float Hold = 4f;

        /// <summary>How long one leg takes at the speed and distance above.</summary>
        private const float LegSeconds = Distance / Speed;

        /// <summary>Out, rest, back, rest.</summary>
        private const float CycleSeconds = (LegSeconds + Hold) * 2f;

        [Test]
        public void ResolveOffset_StartsHomeAndComesBackToIt()
        {
            Assert.That(
                Offset(0f),
                Is.EqualTo(0f).Within(0.0001f),
                "A cloud must be first seen where it was authored, not mid-drift.");

            Assert.That(
                Offset(LegSeconds + Hold + LegSeconds),
                Is.EqualTo(0f).Within(0.0001f),
                "The return leg has to land back home, or the cloud walks away over time.");

            Assert.That(Offset(CycleSeconds), Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void ResolveOffset_TurnsBackAtTheDistanceItIsGiven()
        {
            Assert.That(Offset(LegSeconds), Is.EqualTo(Distance).Within(0.0001f));

            Assert.That(
                Offset(LegSeconds + Hold + LegSeconds * 0.5f),
                Is.EqualTo(Distance * 0.5f).Within(0.0001f),
                "Half way through the return leg is half way home.");
        }

        /// <summary>
        /// The rest is what draws the two legs apart. Without it the cloud is still eased, but the
        /// eased-out and the eased-in meet at one instant and the turn still reads as a bounce.
        /// </summary>
        [Test]
        public void ResolveOffset_RestsAtEachEndBeforeSettingOffAgain()
        {
            for (int step = 0; step <= 8; step++)
            {
                float into = Hold * (step / 8f);

                Assert.That(
                    Offset(LegSeconds + into),
                    Is.EqualTo(Distance).Within(0.0001f),
                    "The cloud left the far end " + into + "s into its rest.");

                Assert.That(
                    Offset(LegSeconds + Hold + LegSeconds + into),
                    Is.EqualTo(0f).Within(0.0001f),
                    "The cloud left home " + into + "s into its rest.");
            }
        }

        /// <summary>
        /// Measured as distance covered per tick rather than as a slope, because what reads as a
        /// hard turn is exactly that: the cloud still covering ground at the moment it reverses.
        /// </summary>
        [Test]
        public void ResolveOffset_EasesIntoTheTurnInsteadOfArrivingAtSpeed()
        {
            const float tick = 0.1f;
            float atTheTurn = Offset(LegSeconds) - Offset(LegSeconds - tick);
            float midLeg = Offset(LegSeconds * 0.5f) - Offset(LegSeconds * 0.5f - tick);

            Assert.That(
                atTheTurn,
                Is.LessThan(midLeg * 0.25f),
                "The cloud is covering " + atTheTurn + " units a tick as it reaches the turn "
                + "against " + midLeg + " mid-leg, so it is arriving at close to full speed.");

            float leavingTheTurn = Offset(LegSeconds + Hold + tick) - Offset(LegSeconds + Hold);
            Assert.That(
                -leavingTheTurn,
                Is.LessThan(midLeg * 0.25f),
                "The cloud sets off from its rest at close to full speed.");
        }

        /// <summary>
        /// Swept across several cycles rather than checked at the turns, because the failure this
        /// guards against - a cloud that creeps a little further each time round - only shows up
        /// away from the moments the arithmetic is obviously right at.
        /// </summary>
        [Test]
        public void ResolveOffset_NeverLeavesTheSideOfHomeItWasSentTo()
        {
            for (int step = 0; step <= 400; step++)
            {
                float elapsed = step * 0.25f;
                float offset = CloudDrift.ResolveOffset(elapsed, Speed, Distance, 0.3f, Hold);

                Assert.That(
                    offset,
                    Is.InRange(0f, Distance),
                    "At " + elapsed + "s the cloud is " + offset + " from home, outside the "
                    + Distance + " it was given.");
            }
        }

        /// <summary>
        /// Clouds on the same drift move as one sheet, which reads as the backdrop sliding rather
        /// than as weather. The phase is the only thing that breaks that up.
        /// </summary>
        [Test]
        public void ResolveOffset_StaggersTwoCloudsGivenTheSameDrift()
        {
            Assert.That(
                CloudDrift.ResolveOffset(0f, Speed, Distance, 0f, Hold),
                Is.EqualTo(0f).Within(0.0001f));

            Assert.That(
                CloudDrift.ResolveOffset(0f, Speed, Distance, 0.5f, Hold),
                Is.EqualTo(Distance).Within(0.0001f),
                "Half a cycle in is the far end, where the other cloud has yet to reach.");
        }

        /// <summary>A cloud told not to rest still eases, and still comes home.</summary>
        [Test]
        public void ResolveOffset_HandlesADriftWithNoRestAtTheEnds()
        {
            Assert.That(
                CloudDrift.ResolveOffset(LegSeconds, Speed, Distance, 0f, 0f),
                Is.EqualTo(Distance).Within(0.0001f));

            Assert.That(
                CloudDrift.ResolveOffset(LegSeconds * 2f, Speed, Distance, 0f, 0f),
                Is.EqualTo(0f).Within(0.0001f));
        }

        /// <summary>
        /// Zero is a live authoring value - it is what a cloud gets while its drift is being tuned
        /// - and the leg it spans is what the travel would be divided by.
        /// </summary>
        [Test]
        public void ResolveOffset_LeavesACloudWithNowhereToGoAtHome()
        {
            Assert.That(CloudDrift.ResolveOffset(12f, Speed, 0f, 0f, Hold), Is.EqualTo(0f));
            Assert.That(CloudDrift.ResolveOffset(12f, 0f, Distance, 0f, Hold), Is.EqualTo(0f));
            Assert.That(CloudDrift.ResolveOffset(12f, -4f, -20f, 0f, Hold), Is.EqualTo(0f));
        }

        private static float Offset(float elapsedSeconds)
        {
            return CloudDrift.ResolveOffset(elapsedSeconds, Speed, Distance, 0f, Hold);
        }
    }
}
