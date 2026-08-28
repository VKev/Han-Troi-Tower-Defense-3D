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

        public event Action<long> StepCompleted;

        public float InterpolationAlpha => clock.InterpolationAlpha;
        public long CurrentStep { get; private set; }
        public bool IsPaused { get; private set; }

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

            clock.Advance(deltaTimeSeconds, Step);
        }

        public void Reset()
        {
            IsPaused = false;
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
