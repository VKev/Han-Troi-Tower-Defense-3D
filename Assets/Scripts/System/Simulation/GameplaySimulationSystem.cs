using System;
using TowerDefense3D.Core;
using TowerDefense3D.Enemies;
using TowerDefense3D.Towers;
using TowerDefense3D.Waves;

namespace TowerDefense3D.Simulation
{
    /// <summary>
    /// Owns the level fixed-step order for wave, tower, enemy and hit simulation.
    /// </summary>
    public sealed class GameplaySimulationSystem
    {
        private readonly WaveSystem waveSystem;
        private readonly TowerNetworkManager towerNetworkManager;
        private readonly CombatTimelineSystem combatTimelineSystem;
        private readonly FixedStepClock clock;

        public GameplaySimulationSystem(
            WaveSystem waveSystem,
            TowerNetworkManager towerNetworkManager,
            CombatTimelineSystem combatTimelineSystem)
        {
            this.waveSystem = waveSystem ?? throw new ArgumentNullException(nameof(waveSystem));
            this.towerNetworkManager = towerNetworkManager
                ?? throw new ArgumentNullException(nameof(towerNetworkManager));
            this.combatTimelineSystem = combatTimelineSystem
                ?? throw new ArgumentNullException(nameof(combatTimelineSystem));
            clock = new FixedStepClock(towerNetworkManager.TickSeconds);
        }

        /// <summary>How much faster than real time the simulation runs while fast-forwarding.</summary>
        public const float FastForwardMultiplier = 2f;

        public event Action<long> StepCompleted;

        public float InterpolationAlpha => clock.InterpolationAlpha;
        public long CurrentStep { get; private set; }
        public bool IsPaused { get; private set; }
        public bool IsFastForward { get; private set; }

        private float tutorialTimeScale = 1f;

        public void Tick(float deltaTimeSeconds)
        {
            if (IsPaused)
            {
                return;
            }

            if (!waveSystem.IsRunning)
            {
                clock.Reset();
                return;
            }

            // Fast-forward feeds the clock more seconds than actually passed, so it runs more
            // fixed steps this frame. Everything downstream - spawning, tower cycles, the combat
            // timeline - is driven by step count, so they all speed up together and stay in step
            // with each other. Scaling a speed somewhere would have desynchronised them.
            //
            // The step itself is never shortened. Shortening it would change the tick rate the
            // combat timeline was planned against, and the plan is what the wave is replaying.
            clock.Advance(deltaTimeSeconds * SpeedMultiplier, Step);
        }

        /// <summary>
        /// How fast the simulation is running against real time. Presentation reads it so that
        /// anything Unity drives on its own clock - animator playback, for one - keeps pace with
        /// the simulation instead of drifting behind it.
        /// </summary>
        public float SpeedMultiplier =>
            (IsFastForward ? FastForwardMultiplier : 1f) * tutorialTimeScale;

        /// <summary>
        /// Runs the simulation slower without stopping it, for a tutorial beat that has to be
        /// watched happening rather than pointed at while frozen.
        /// </summary>
        /// <remarks>
        /// Multiplied into the speed rather than replacing it, so a beat that slows the game does
        /// not quietly cancel the player's own double-speed and hand it back wrong afterwards.
        /// Zero is refused: stopping is what <see cref="SetPaused"/> is for, and a zero here would
        /// stall the clock somewhere no pause flag records.
        /// </remarks>
        public void SetTutorialTimeScale(float scale)
        {
            tutorialTimeScale = scale <= 0f ? 1f : scale;
        }

        /// <summary>
        /// Turns double speed on or off.
        /// </summary>
        /// <remarks>
        /// Anything that interpolates against this clock follows on its own, because it is reading
        /// the faster steps. Anything Unity drives on its own clock does not, and has to be told
        /// through <see cref="SpeedMultiplier"/> - enemy animators are the case in point.
        /// </remarks>
        public void SetFastForward(bool isFastForward)
        {
            IsFastForward = isFastForward;
        }

        public void Reset()
        {
            IsPaused = false;
            IsFastForward = false;
            tutorialTimeScale = 1f;
            clock.Reset();
            combatTimelineSystem.Reset();
            CurrentStep = 0L;
        }

        public void SetPaused(bool isPaused)
        {
            IsPaused = isPaused;
        }

        private void Step()
        {
            if (!waveSystem.IsRunning)
            {
                return;
            }

            waveSystem.StepSpawning(clock.StepSeconds);
            towerNetworkManager.StepOneTick();
            combatTimelineSystem.Step();
            waveSystem.CompleteStep();
            CurrentStep++;
            StepCompleted?.Invoke(CurrentStep);
        }
    }
}
