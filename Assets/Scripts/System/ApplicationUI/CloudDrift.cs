using UnityEngine;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// How far a cloud has wandered from where it was authored, at a given moment.
    ///
    /// A cloud sets off, travels <c>distance</c>, rests, comes back to the spot it started from,
    /// rests again - then does it again. The turn is what keeps the sky alive without the clouds
    /// ever leaving it: a cloud that only ever drifts one way eventually sails off the screen and
    /// the backdrop is left bare.
    ///
    /// The turn itself is the part that has to be handled carefully. Wind does not reverse at
    /// speed, so a cloud that hits its far end and immediately heads back at the same rate reads
    /// as a mechanism rather than as weather. Each leg is eased instead - the cloud is slowest at
    /// both ends of it - and the rest at each end draws the two legs apart so the eye never sees
    /// one turn into the other.
    ///
    /// The offset returned is never negative, so a cloud only ever moves to one side of where it
    /// was placed. That is what lets a cloud be authored clear of a level node and stay clear of
    /// it, rather than swinging back across the node on the return leg.
    /// </summary>
    public static class CloudDrift
    {
        /// <summary>
        /// How far from home the cloud is after <paramref name="elapsedSeconds"/>, in the same
        /// units the cloud is positioned in. Which side of home that lands on is the caller's to
        /// decide - this is the distance, not the direction.
        /// </summary>
        /// <param name="speed">
        /// Units per second averaged over a leg. The leg is eased rather than flat, so the cloud
        /// is quicker than this in the middle of it and slower at either end, and a leg still
        /// takes exactly the time this rate says it should.
        /// </param>
        /// <param name="distance">How far the cloud goes before it turns back.</param>
        /// <param name="phase">
        /// Where in the round trip the cloud starts, as a share of one full out-and-back. Clouds
        /// given the same drift move in lockstep, which reads as a single sheet sliding rather
        /// than as weather; different phases are what break that up.
        /// </param>
        /// <param name="holdSeconds">How long the cloud rests at each end before it sets off again.</param>
        public static float ResolveOffset(
            float elapsedSeconds,
            float speed,
            float distance,
            float phase,
            float holdSeconds)
        {
            // A cloud with nowhere to go, or no rate to get there at, simply sits where it was
            // authored. Worth returning rather than computing: the leg below would otherwise be a
            // division by zero, and a NaN anchoredPosition takes the cloud off the screen for good.
            if (distance <= 0f || speed <= 0f)
            {
                return 0f;
            }

            float legSeconds = distance / speed;
            float hold = Mathf.Max(0f, holdSeconds);
            float cycleSeconds = (legSeconds + hold) * 2f;

            // Out, rest, back, rest: one whole cycle, which is also the span a phase of one shifts
            // the cloud along by.
            float atSeconds = Mathf.Repeat(elapsedSeconds + phase * cycleSeconds, cycleSeconds);

            if (atSeconds < legSeconds)
            {
                // Smoothed rather than linear so the cloud is barely moving as it arrives at the
                // far end, and picks up again gently on the way out.
                return Mathf.SmoothStep(0f, distance, atSeconds / legSeconds);
            }

            atSeconds -= legSeconds;
            if (atSeconds < hold)
            {
                return distance;
            }

            atSeconds -= hold;
            if (atSeconds < legSeconds)
            {
                return Mathf.SmoothStep(distance, 0f, atSeconds / legSeconds);
            }

            return 0f;
        }
    }
}
