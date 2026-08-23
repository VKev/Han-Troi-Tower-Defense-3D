using TowerDefense3D.GridPlacement;
using TowerDefense3D.Mobile;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Owns the explicit lifecycle order for application-scoped systems.
    /// </summary>
    public sealed class ApplicationSystemGroup
    {
        private readonly FramePacingSystem framePacingSystem;
        private readonly SafeAreaSystem safeAreaSystem;
        private readonly GameFlowCoordinator gameFlowSystem;

        public ApplicationSystemGroup(
            FramePacingSystem framePacingSystem,
            SafeAreaSystem safeAreaSystem,
            GameFlowCoordinator gameFlowSystem)
        {
            this.framePacingSystem = framePacingSystem;
            this.safeAreaSystem = safeAreaSystem;
            this.gameFlowSystem = gameFlowSystem;
        }

        public void Start()
        {
            framePacingSystem.Start();
            safeAreaSystem.Start();
            gameFlowSystem.Start();
        }

        public void Tick()
        {
            safeAreaSystem.Tick();
        }

        public void Shutdown()
        {
            gameFlowSystem.Dispose();
        }
    }
}
