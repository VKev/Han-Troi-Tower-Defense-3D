using TowerDefense3D.GameplayInput;
using TowerDefense3D.GridPlacement;

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

        public LevelSystemGroup(
            BoardSystem boardSystem,
            BoardCameraSystem boardCameraSystem,
            GameplayInputSystem gameplayInputSystem,
            GridPlacementSystem gridPlacementSystem)
        {
            this.boardSystem = boardSystem;
            this.boardCameraSystem = boardCameraSystem;
            this.gameplayInputSystem = gameplayInputSystem;
            this.gridPlacementSystem = gridPlacementSystem;
        }

        public void Start()
        {
            boardSystem.Start();
            boardCameraSystem.Start();
            gameplayInputSystem.Start();
        }

        public void Tick(float deltaTime)
        {
            gameplayInputSystem.Tick();
            gridPlacementSystem.Tick();
        }

        public void LateTick(float deltaTime)
        {
            boardCameraSystem.LateTick();
        }
    }
}
