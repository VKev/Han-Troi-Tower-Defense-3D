using TowerDefense3D.GameplayInput;
using TowerDefense3D.GridPlacement;
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
        private readonly GameplayInputSystem gameplayInputSystem;
        private readonly GridPlacementSystem gridPlacementSystem;
        private readonly TowerNetworkSystem towerNetworkSystem;
        private readonly TowerInteractionSystem towerInteractionSystem;
        private readonly TowerSimulationSystem towerSimulationSystem;
        private readonly TowerLinkPresentationSystem towerLinkPresentationSystem;
        private readonly TowerProjectilePresentationSystem towerProjectilePresentationSystem;

        public LevelSystemGroup(
            BoardSystem boardSystem,
            BoardCameraSystem boardCameraSystem,
            GameplayInputSystem gameplayInputSystem,
            GridPlacementSystem gridPlacementSystem,
            TowerNetworkSystem towerNetworkSystem,
            TowerInteractionSystem towerInteractionSystem,
            TowerSimulationSystem towerSimulationSystem,
            TowerLinkPresentationSystem towerLinkPresentationSystem,
            TowerProjectilePresentationSystem towerProjectilePresentationSystem)
        {
            this.boardSystem = boardSystem;
            this.boardCameraSystem = boardCameraSystem;
            this.gameplayInputSystem = gameplayInputSystem;
            this.gridPlacementSystem = gridPlacementSystem;
            this.towerNetworkSystem = towerNetworkSystem;
            this.towerInteractionSystem = towerInteractionSystem;
            this.towerSimulationSystem = towerSimulationSystem;
            this.towerLinkPresentationSystem = towerLinkPresentationSystem;
            this.towerProjectilePresentationSystem = towerProjectilePresentationSystem;
        }

        public void Start()
        {
            boardSystem.Start();
            boardCameraSystem.Start();
            gameplayInputSystem.Start();
            towerNetworkSystem.Start();
            towerLinkPresentationSystem.Start();
            towerProjectilePresentationSystem.Start();
        }

        public void Tick(float deltaTime)
        {
            gameplayInputSystem.Tick();
            gridPlacementSystem.Tick();
            towerInteractionSystem.Tick();
            towerSimulationSystem.Tick(deltaTime);
        }

        public void LateTick(float deltaTime)
        {
            towerLinkPresentationSystem.LateTick();
            towerProjectilePresentationSystem.LateTick();
            boardCameraSystem.LateTick();
        }
    }
}
