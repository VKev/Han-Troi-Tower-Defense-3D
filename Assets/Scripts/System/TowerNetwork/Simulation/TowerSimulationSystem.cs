using System;

namespace TowerDefense3D.Towers
{
    /// <summary>
    /// Converts application frame time into deterministic tower-network simulation ticks.
    /// </summary>
    public sealed class TowerSimulationSystem
    {
        private readonly TowerNetworkManager manager;
        private double accumulatedSeconds;

        public TowerSimulationSystem(TowerNetworkManager manager)
        {
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
        }

        public event Action<long> TickCompleted;

        public double AccumulatedSeconds => accumulatedSeconds;
        public float InterpolationAlpha => CalculateInterpolationAlpha();

        public void Tick(float deltaTimeSeconds)
        {
            AdvanceFrame(deltaTimeSeconds);
        }

        public int AdvanceFrame(float deltaTimeSeconds)
        {
            ValidateDeltaTime(deltaTimeSeconds);

            if (!manager.IsRunning)
            {
                accumulatedSeconds = 0d;
                return 0;
            }

            accumulatedSeconds += deltaTimeSeconds;
            double tickSeconds = manager.TickSeconds;
            int executedTickCount = 0;

            while (manager.IsRunning && accumulatedSeconds >= tickSeconds)
            {
                if (!manager.StepOneTick())
                {
                    break;
                }

                accumulatedSeconds -= tickSeconds;
                executedTickCount++;
                TickCompleted?.Invoke(manager.CurrentTick);
            }

            return executedTickCount;
        }

        public void Reset()
        {
            accumulatedSeconds = 0d;
        }

        private static void ValidateDeltaTime(float deltaTimeSeconds)
        {
            if (float.IsNaN(deltaTimeSeconds) || float.IsInfinity(deltaTimeSeconds) || deltaTimeSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(deltaTimeSeconds), "Frame delta time must be finite and non-negative.");
            }
        }

        private float CalculateInterpolationAlpha()
        {
            if (!manager.IsRunning)
            {
                return 0f;
            }

            double alpha = accumulatedSeconds / manager.TickSeconds;
            return (float)Math.Min(1d, Math.Max(0d, alpha));
        }
    }
}
