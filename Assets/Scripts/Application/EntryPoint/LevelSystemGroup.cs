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

        public LevelSystemGroup(
            BoardSystem boardSystem,
            BoardCameraSystem boardCameraSystem)
        {
            this.boardSystem = boardSystem;
            this.boardCameraSystem = boardCameraSystem;
        }

        public void Start()
        {
            boardSystem.Start();
            boardCameraSystem.Start();
        }

        public void Tick(float deltaTime)
        {
        }

        public void LateTick(float deltaTime)
        {
            boardCameraSystem.LateTick();
        }
    }
}
