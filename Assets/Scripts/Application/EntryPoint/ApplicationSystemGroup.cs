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
        private readonly ApplicationUISystem applicationUISystem;
        private readonly GameFlowSystem gameFlowSystem;

        public ApplicationSystemGroup(
            FramePacingSystem framePacingSystem,
            SafeAreaSystem safeAreaSystem,
            ApplicationUISystem applicationUISystem,
            GameFlowSystem gameFlowSystem)
        {
            this.framePacingSystem = framePacingSystem;
            this.safeAreaSystem = safeAreaSystem;
            this.applicationUISystem = applicationUISystem;
            this.gameFlowSystem = gameFlowSystem;
        }

        public void Start()
        {
            framePacingSystem.Start();
            safeAreaSystem.Start();
            applicationUISystem.Start();
            try
            {
                gameFlowSystem.Start();
            }
            catch
            {
                applicationUISystem.Dispose();
                throw;
            }
        }

        public void Tick()
        {
            safeAreaSystem.Tick();
        }

        public void Shutdown()
        {
            gameFlowSystem.Dispose();
            applicationUISystem.Dispose();
        }
    }
}
