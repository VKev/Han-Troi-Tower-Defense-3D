using TowerDefense3D.Audio;
using TowerDefense3D.GridPlacement;
using TowerDefense3D.Mobile;
using TowerDefense3D.Tutorials;

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
        private readonly LobbyMusicSystem lobbyMusicSystem;
        private readonly TutorialSystem tutorialSystem;
        private readonly ITutorialOverlay tutorialOverlay;
        private readonly ITutorialInputGate tutorialInputGate;

        public ApplicationSystemGroup(
            FramePacingSystem framePacingSystem,
            SafeAreaSystem safeAreaSystem,
            ApplicationUISystem applicationUISystem,
            GameFlowSystem gameFlowSystem,
            LobbyMusicSystem lobbyMusicSystem,
            TutorialSystem tutorialSystem,
            ITutorialOverlay tutorialOverlay,
            ITutorialInputGate tutorialInputGate)
        {
            this.framePacingSystem = framePacingSystem;
            this.safeAreaSystem = safeAreaSystem;
            this.applicationUISystem = applicationUISystem;
            this.gameFlowSystem = gameFlowSystem;
            this.lobbyMusicSystem = lobbyMusicSystem;
            this.tutorialSystem = tutorialSystem;
            this.tutorialOverlay = tutorialOverlay;
            this.tutorialInputGate = tutorialInputGate;
        }

        public ApplicationSystemGroup(
            FramePacingSystem framePacingSystem,
            SafeAreaSystem safeAreaSystem,
            ApplicationUISystem applicationUISystem,
            GameFlowSystem gameFlowSystem)
            : this(
                framePacingSystem,
                safeAreaSystem,
                applicationUISystem,
                gameFlowSystem,
                null,
                null,
                null,
                null)
        {
        }

        public void Start()
        {
            framePacingSystem.Start();
            safeAreaSystem.Start();
            applicationUISystem.Start();
            tutorialSystem?.Bind(tutorialOverlay, tutorialInputGate);
            lobbyMusicSystem?.Start();
            try
            {
                gameFlowSystem.Start();
            }
            catch
            {
                lobbyMusicSystem?.Dispose();
                applicationUISystem.Dispose();
                throw;
            }
        }

        public void Tick(float deltaTime)
        {
            safeAreaSystem.Tick();
            tutorialSystem.Tick(deltaTime);
        }

        public void Shutdown()
        {
            gameFlowSystem.Dispose();
            lobbyMusicSystem?.Dispose();
            applicationUISystem.Dispose();
        }
    }
}
