using System;

namespace TowerDefense3D.Mobile
{
    /// <summary>
    /// Averages the frame rate over a window rather than reporting each frame on its own.
    ///
    /// A per-frame reading swings between wildly different numbers several times a second, which is
    /// unreadable and hides the thing worth seeing - whether the game is holding its target. So
    /// frames are counted until enough time has passed, and the average over that stretch is what
    /// gets reported.
    /// </summary>
    public sealed class FrameRateSampler
    {
        private readonly float windowSeconds;
        private int frames;
        private float elapsedSeconds;

        public FrameRateSampler(float windowSeconds)
        {
            if (windowSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(windowSeconds),
                    windowSeconds,
                    "A sampling window needs a positive length.");
            }

            this.windowSeconds = windowSeconds;
        }

        /// <summary>
        /// Counts one rendered frame. A frame reported as taking no time, or as taking a time that
        /// is not a number, is dropped: neither says anything about how fast the game is running,
        /// and either would poison the average.
        /// </summary>
        public void Add(float deltaSeconds)
        {
            if (deltaSeconds <= 0f || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds))
            {
                return;
            }

            frames++;
            elapsedSeconds += deltaSeconds;
        }

        /// <summary>
        /// The average over the window just finished, if one has. Taking it clears the count, so
        /// the next window starts fresh and no frame is counted twice.
        /// </summary>
        public bool TryTakeAverage(out float framesPerSecond)
        {
            if (elapsedSeconds < windowSeconds)
            {
                framesPerSecond = 0f;
                return false;
            }

            framesPerSecond = frames / elapsedSeconds;
            frames = 0;
            elapsedSeconds = 0f;
            return true;
        }
    }
}
