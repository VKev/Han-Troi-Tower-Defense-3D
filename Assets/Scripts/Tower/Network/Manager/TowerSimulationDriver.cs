using System;
using UnityEngine;

namespace TowerDefense3D.Towers
{
    [DisallowMultipleComponent]
    public sealed class TowerSimulationDriver : MonoBehaviour
    {
        private TowerNetworkManager manager;
        private double accumulatedSeconds;

        public event Action<long> TickCompleted;

        public bool IsInitialized => manager != null;
        public double AccumulatedSeconds => accumulatedSeconds;
        public float InterpolationAlpha => CalculateInterpolationAlpha();

        public void Initialize(TowerNetworkManager towerNetworkManager)
        {
            if (towerNetworkManager == null)
            {
                throw new ArgumentNullException(nameof(towerNetworkManager));
            }

            if (manager != null)
            {
                if (ReferenceEquals(manager, towerNetworkManager))
                {
                    return;
                }

                throw new InvalidOperationException(
                    "TowerSimulationDriver is already initialized with another manager.");
            }

            manager = towerNetworkManager;
            accumulatedSeconds = 0d;
        }

        public void Shutdown()
        {
            accumulatedSeconds = 0d;
            manager = null;
        }

        public int AdvanceFrame(float deltaTimeSeconds)
        {
            ValidateDeltaTime(deltaTimeSeconds);

            if (manager == null)
            {
                throw new InvalidOperationException("TowerSimulationDriver must be initialized before advancing.");
            }

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

        private void Update()
        {
            if (manager != null)
            {
                AdvanceFrame(Time.deltaTime);
            }
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
            if (manager == null || !manager.IsRunning)
            {
                return 0f;
            }

            return Mathf.Clamp01((float)(accumulatedSeconds / manager.TickSeconds));
        }
    }
}
