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
        private readonly EnemySystem enemySystem;
        private readonly ProjectileHitSystem projectileHitSystem;
        private readonly FixedStepClock clock;

        public GameplaySimulationSystem(
            WaveSystem waveSystem,
            TowerNetworkManager towerNetworkManager,
            EnemySystem enemySystem,
            ProjectileHitSystem projectileHitSystem)
        {
            this.waveSystem = waveSystem ?? throw new ArgumentNullException(nameof(waveSystem));
            this.towerNetworkManager = towerNetworkManager
                ?? throw new ArgumentNullException(nameof(towerNetworkManager));
            this.enemySystem = enemySystem ?? throw new ArgumentNullException(nameof(enemySystem));
            this.projectileHitSystem = projectileHitSystem
                ?? throw new ArgumentNullException(nameof(projectileHitSystem));
            clock = new FixedStepClock(towerNetworkManager.TickSeconds);
        }

        public event Action<long> StepCompleted;

        public float InterpolationAlpha => clock.InterpolationAlpha;
        public long CurrentStep { get; private set; }

        public void Tick(float deltaTimeSeconds)
        {
            if (!waveSystem.IsRunning)
            {
                clock.Reset();
                return;
            }

            clock.Advance(deltaTimeSeconds, Step);
        }

        public void Reset()
        {
            clock.Reset();
            projectileHitSystem.Reset();
            CurrentStep = 0L;
        }

        private void Step()
        {
            if (!waveSystem.IsRunning)
            {
                return;
            }

            waveSystem.StepSpawning(clock.StepSeconds);
            towerNetworkManager.StepOneTick();
            enemySystem.Step(clock.StepSeconds);
            projectileHitSystem.Step();
            waveSystem.CompleteStep();
            CurrentStep++;
            StepCompleted?.Invoke(CurrentStep);
        }
    }
}
