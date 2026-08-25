using System;

namespace TowerDefense3D.Simulation
{
    /// <summary>
    /// Converts frame time into deterministic fixed simulation steps.
    /// </summary>
    public sealed class FixedStepClock
    {
        private const double StepToleranceSeconds = 0.0000001d;
        private readonly double stepSeconds;
        private double accumulatedSeconds;

        public FixedStepClock(float stepSeconds)
        {
            if (float.IsNaN(stepSeconds) || float.IsInfinity(stepSeconds) || stepSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(stepSeconds), "Step duration must be finite and positive.");
            }

            this.stepSeconds = stepSeconds;
        }

        public double AccumulatedSeconds => accumulatedSeconds;
        public float StepSeconds => (float)stepSeconds;
        public float InterpolationAlpha => (float)Math.Min(1d, accumulatedSeconds / stepSeconds);

        public int Advance(float deltaTimeSeconds, Action step)
        {
            if (float.IsNaN(deltaTimeSeconds) || float.IsInfinity(deltaTimeSeconds)
                || deltaTimeSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(deltaTimeSeconds), "Frame delta time must be finite and non-negative.");
            }

            if (step == null)
            {
                throw new ArgumentNullException(nameof(step));
            }

            accumulatedSeconds += deltaTimeSeconds;
            int stepCount = 0;
            while (accumulatedSeconds + StepToleranceSeconds >= stepSeconds)
            {
                step();
                accumulatedSeconds -= stepSeconds;
                if (accumulatedSeconds < 0d)
                {
                    accumulatedSeconds = 0d;
                }

                stepCount++;
            }

            return stepCount;
        }

        public void Reset()
        {
            accumulatedSeconds = 0d;
        }
    }
}
