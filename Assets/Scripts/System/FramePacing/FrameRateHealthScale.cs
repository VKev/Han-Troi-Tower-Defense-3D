namespace TowerDefense3D.Mobile
{
    /// <summary>How a measured frame rate stands against the rate the game is pacing itself to.</summary>
    public enum FrameRateHealth
    {
        Poor,
        Fair,
        Good
    }

    /// <summary>
    /// Reads a frame rate against the target. A bare number means little at a glance - whether 47
    /// is fine depends entirely on what the game is aiming for - so the readout is graded, and the
    /// grade is what carries the colour.
    /// </summary>
    public static class FrameRateHealthScale
    {
        /// <summary>Within a tenth of target is as good as on target once rounding is allowed for.</summary>
        public const float GoodShareOfTarget = 0.9f;

        /// <summary>Below about three fifths of target the drop is plain to feel, not just to measure.</summary>
        public const float FairShareOfTarget = 0.6f;

        public static FrameRateHealth Resolve(float framesPerSecond, int targetFrameRate)
        {
            if (targetFrameRate <= 0)
            {
                return FrameRateHealth.Good;
            }

            float share = framesPerSecond / targetFrameRate;
            if (share >= GoodShareOfTarget)
            {
                return FrameRateHealth.Good;
            }

            return share >= FairShareOfTarget ? FrameRateHealth.Fair : FrameRateHealth.Poor;
        }
    }
}
