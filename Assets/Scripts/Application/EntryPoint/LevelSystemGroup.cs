using TowerDefense3D.Enemies;
using TowerDefense3D.Audio;
using TowerDefense3D.GameplayInput;
using TowerDefense3D.GridPlacement;
using TowerDefense3D.Simulation;
using TowerDefense3D.Towers;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Explicit dispatch surface populated by the active level scope.
    /// </summary>
    public sealed class LevelSystemGroup
    {
        private readonly BoardSystem boardSystem;
        private readonly BoardCameraSystem boardCameraSystem;
        private readonly BoardCameraGestureSystem boardCameraGestureSystem;
        private readonly GameplayInputSystem gameplayInputSystem;
        private readonly GridPlacementSystem gridPlacementSystem;
        private readonly TowerNetworkSystem towerNetworkSystem;
        private readonly TowerInteractionSystem towerInteractionSystem;
        private readonly GameplaySimulationSystem gameplaySimulationSystem;
        private readonly EnemyPresentationSystem enemyPresentationSystem;
        private readonly HeroAttackPresentationSystem heroAttackPresentationSystem;
        private readonly TowerLinkPresentationSystem towerLinkPresentationSystem;
        private readonly TowerProjectilePresentationSystem towerProjectilePresentationSystem;
        private readonly TowerTierVisualPresentationSystem towerTierVisualPresentationSystem;
        private readonly GameplayUISystem gameplayUISystem;
        private readonly SoundCueSystem soundCueSystem;
        private readonly LevelPreparationMusicSystem levelPreparationMusicSystem;
        private readonly SafeAreaSystem safeAreaSystem;

        public LevelSystemGroup(
            BoardSystem boardSystem,
            BoardCameraSystem boardCameraSystem,
            BoardCameraGestureSystem boardCameraGestureSystem,
            GameplayInputSystem gameplayInputSystem,
            GridPlacementSystem gridPlacementSystem,
            TowerNetworkSystem towerNetworkSystem,
            TowerInteractionSystem towerInteractionSystem,
            GameplaySimulationSystem gameplaySimulationSystem,
            EnemyPresentationSystem enemyPresentationSystem,
            HeroAttackPresentationSystem heroAttackPresentationSystem,
            TowerLinkPresentationSystem towerLinkPresentationSystem,
            TowerProjectilePresentationSystem towerProjectilePresentationSystem,
            TowerTierVisualPresentationSystem towerTierVisualPresentationSystem,
            GameplayUISystem gameplayUISystem,
            SoundCueSystem soundCueSystem,
            LevelPreparationMusicSystem levelPreparationMusicSystem,
            SafeAreaSystem safeAreaSystem)
        {
            this.boardSystem = boardSystem;
            this.boardCameraSystem = boardCameraSystem;
            this.boardCameraGestureSystem = boardCameraGestureSystem;
            this.gameplayInputSystem = gameplayInputSystem;
            this.gridPlacementSystem = gridPlacementSystem;
            this.towerNetworkSystem = towerNetworkSystem;
            this.towerInteractionSystem = towerInteractionSystem;
            this.gameplaySimulationSystem = gameplaySimulationSystem;
            this.enemyPresentationSystem = enemyPresentationSystem;
            this.heroAttackPresentationSystem = heroAttackPresentationSystem;
            this.towerLinkPresentationSystem = towerLinkPresentationSystem;
            this.towerProjectilePresentationSystem = towerProjectilePresentationSystem;
            this.towerTierVisualPresentationSystem = towerTierVisualPresentationSystem;
            this.gameplayUISystem = gameplayUISystem;
            this.soundCueSystem = soundCueSystem;
            this.levelPreparationMusicSystem = levelPreparationMusicSystem;
            this.safeAreaSystem = safeAreaSystem;
        }

        public void Start()
        {
            boardSystem.Start();
            boardCameraSystem.Start();
            gameplayInputSystem.Start();
            boardCameraGestureSystem.Start();
            towerNetworkSystem.Start();
            towerLinkPresentationSystem.Start();
            towerProjectilePresentationSystem.Start();
            towerTierVisualPresentationSystem.Start();
            enemyPresentationSystem.Start();
            heroAttackPresentationSystem.Start();
            soundCueSystem.Start();
            levelPreparationMusicSystem.Start();
            gameplayUISystem.Start();
            safeAreaSystem.Start();
        }

        public void Tick(float deltaTime)
        {
            // Polled every frame: the device safe area changes when the screen rotates or the
            // Device Simulator swaps device, and the level HUD has to follow it.
            safeAreaSystem.Tick();
            gameplayInputSystem.Tick();

            // Ahead of the other pointer consumers: a pinch or a pan claims the input mode, and
            // placement and tower picking both stand down for a mode that is not their own.
            boardCameraGestureSystem.Tick(deltaTime);
            gridPlacementSystem.Tick();
            towerInteractionSystem.Tick();
            gameplaySimulationSystem.Tick(deltaTime);
            gameplayUISystem.RefreshIfDirty();
        }

        public void LateTick(float deltaTime)
        {
            towerLinkPresentationSystem.LateTick();
            towerProjectilePresentationSystem.LateTick(deltaTime);
            enemyPresentationSystem.LateTick(gameplaySimulationSystem.InterpolationAlpha);
            boardCameraSystem.LateTick();
        }
    }
}
