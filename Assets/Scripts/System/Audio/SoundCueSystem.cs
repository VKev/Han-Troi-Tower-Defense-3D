using System;
using TowerDefense3D.Economy;
using TowerDefense3D.Enemies;
using TowerDefense3D.GridPlacement;
using TowerDefense3D.Simulation;
using TowerDefense3D.Towers;
using TowerDefense3D.Waves;

namespace TowerDefense3D.Audio
{
    public sealed class SoundCueSystem : IDisposable
    {
        private readonly ISoundPlayer soundPlayer;
        private readonly GridPlacementSystem gridPlacementSystem;
        private readonly TowerNetworkSystem towerNetworkSystem;
        private readonly IWaveSystem waveSystem;
        private readonly CombatTimelineSystem combatTimelineSystem;
        private readonly LevelBaseHealthSystem healthSystem;
        private bool wasWaveRunning;
        private int observedHealth;
        private bool isStarted;

        public SoundCueSystem(
            ISoundPlayer soundPlayer,
            GridPlacementSystem gridPlacementSystem,
            TowerNetworkSystem towerNetworkSystem,
            IWaveSystem waveSystem,
            CombatTimelineSystem combatTimelineSystem,
            LevelBaseHealthSystem healthSystem)
        {
            this.soundPlayer = soundPlayer ?? throw new ArgumentNullException(nameof(soundPlayer));
            this.gridPlacementSystem = gridPlacementSystem
                ?? throw new ArgumentNullException(nameof(gridPlacementSystem));
            this.towerNetworkSystem = towerNetworkSystem
                ?? throw new ArgumentNullException(nameof(towerNetworkSystem));
            this.waveSystem = waveSystem ?? throw new ArgumentNullException(nameof(waveSystem));
            this.combatTimelineSystem = combatTimelineSystem
                ?? throw new ArgumentNullException(nameof(combatTimelineSystem));
            this.healthSystem = healthSystem ?? throw new ArgumentNullException(nameof(healthSystem));
        }

        public void Start()
        {
            if (isStarted)
            {
                return;
            }

            wasWaveRunning = waveSystem.IsRunning;
            observedHealth = healthSystem.CurrentHealth;
            gridPlacementSystem.TowerPlaced += HandleTowerPlaced;
            towerNetworkSystem.ProjectileCreated += HandleProjectileCreated;
            towerNetworkSystem.TowerUpgraded += HandleTowerUpgraded;
            towerNetworkSystem.TowerSold += HandleTowerSold;
            waveSystem.StateChanged += HandleWaveStateChanged;
            combatTimelineSystem.ProjectileImpacted += HandleProjectileImpacted;
            combatTimelineSystem.HeroAttackStarted += HandleHeroAttackStarted;
            combatTimelineSystem.ReactionTriggered += HandleReactionTriggered;
            healthSystem.HealthChanged += HandleHealthChanged;
            isStarted = true;
        }

        public void Dispose()
        {
            if (!isStarted)
            {
                return;
            }

            isStarted = false;
            gridPlacementSystem.TowerPlaced -= HandleTowerPlaced;
            towerNetworkSystem.ProjectileCreated -= HandleProjectileCreated;
            towerNetworkSystem.TowerUpgraded -= HandleTowerUpgraded;
            towerNetworkSystem.TowerSold -= HandleTowerSold;
            waveSystem.StateChanged -= HandleWaveStateChanged;
            combatTimelineSystem.ProjectileImpacted -= HandleProjectileImpacted;
            combatTimelineSystem.HeroAttackStarted -= HandleHeroAttackStarted;
            combatTimelineSystem.ReactionTriggered -= HandleReactionTriggered;
            healthSystem.HealthChanged -= HandleHealthChanged;
        }

        private void HandleTowerPlaced(GridPlacementCommit placement)
        {
            _ = placement;
            soundPlayer.Play(SoundId.TowerPlaced);
        }

        private void HandleTowerUpgraded(ITowerRuntimeView tower)
        {
            _ = tower;
            soundPlayer.Play(SoundId.TowerUpgraded);
        }

        private void HandleTowerSold()
        {
            soundPlayer.Play(SoundId.TowerSold);
        }

        private void HandleProjectileCreated(TowerFamily family)
        {
            soundPlayer.Play(family switch
            {
                TowerFamily.Generator => SoundId.GeneratorFired,
                TowerFamily.Water => SoundId.WaterFired,
                TowerFamily.Wind => SoundId.WindFired,
                TowerFamily.Fire => SoundId.FireFired,
                _ => SoundId.None
            });
        }

        private void HandleWaveStateChanged()
        {
            bool isWaveRunning = waveSystem.IsRunning;
            if (!wasWaveRunning && isWaveRunning)
            {
                soundPlayer.Play(SoundId.WaveStarted);
            }
            else if (wasWaveRunning && !isWaveRunning)
            {
                // Once, on the edge, rather than off the wave state itself: the state is
                // republished several times as a wave winds down, and every one of those would
                // otherwise be another copy of the same fanfare.
                soundPlayer.Play(SoundId.WaveEnded);
            }

            wasWaveRunning = isWaveRunning;
        }

        private void HandleProjectileImpacted(ProjectileImpactEvent impact)
        {
            _ = impact;
            soundPlayer.Play(SoundId.ProjectileImpact);
        }

        private void HandleHeroAttackStarted(HeroAttackEvent attack)
        {
            _ = attack;
            soundPlayer.Play(SoundId.HeroAttack);
        }

        private void HandleReactionTriggered(ElementReactionEvent reaction)
        {
            soundPlayer.Play(reaction.ReactionId switch
            {
                ElementReactionId.ThermalShock => SoundId.ThermalShock,
                ElementReactionId.Firestorm => SoundId.Firestorm,
                ElementReactionId.WaterLift => SoundId.WaterLift,
                _ => SoundId.None
            });
        }

        private void HandleHealthChanged(int currentHealth, int maximumHealth)
        {
            _ = maximumHealth;
            if (currentHealth < observedHealth)
            {
                soundPlayer.Play(SoundId.FrogDamaged);
            }

            observedHealth = currentHealth;
        }
    }
}
