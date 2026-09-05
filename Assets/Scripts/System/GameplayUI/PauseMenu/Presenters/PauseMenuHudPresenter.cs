using System;
using TowerDefense3D.Simulation;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Shows the pause modal for exactly as long as the simulation is paused, and routes its
    /// resume, restart, and return-to-menu commands onward.
    /// </summary>
    /// <remarks>
    /// The pause state is read from the simulation rather than tracked here, so the modal cannot
    /// drift out of step with whatever else pauses the game.
    ///
    /// Resume is republished rather than acted on: unpausing also has to flip the pause button's
    /// glyph, and that button belongs to another view, so the one place that owns both - the
    /// gameplay UI system - does it.
    /// </remarks>
    public sealed class PauseMenuHudPresenter
    {
        private readonly GameplaySimulationSystem simulationSystem;
        private readonly IPauseMenuHudView view;

        private Action requestReplayLevel;
        private Action requestReturnToLevelMenu;

        public PauseMenuHudPresenter(
            GameplaySimulationSystem simulationSystem,
            IPauseMenuHudView view)
        {
            this.simulationSystem = simulationSystem
                ?? throw new ArgumentNullException(nameof(simulationSystem));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
        }

        public event Action ResumeRequested;

        public void BindLevel(Action requestReplayLevel, Action requestReturnToLevelMenu)
        {
            this.requestReplayLevel = requestReplayLevel
                ?? throw new ArgumentNullException(nameof(requestReplayLevel));
            this.requestReturnToLevelMenu = requestReturnToLevelMenu
                ?? throw new ArgumentNullException(nameof(requestReturnToLevelMenu));
        }

        public void Connect()
        {
            view.Initialize();
            view.ResumeRequested += HandleResumeRequested;
            view.RestartRequested += HandleRestartRequested;
            view.ReturnToLevelMenuRequested += HandleReturnToLevelMenuRequested;
            Refresh();
        }

        public void Disconnect()
        {
            view.ResumeRequested -= HandleResumeRequested;
            view.RestartRequested -= HandleRestartRequested;
            view.ReturnToLevelMenuRequested -= HandleReturnToLevelMenuRequested;
            view.Shutdown();
        }

        public void Refresh()
        {
            view.Render(new PauseMenuHudState(simulationSystem.IsPaused));
        }

        private void HandleResumeRequested()
        {
            ResumeRequested?.Invoke();
        }

        private void HandleRestartRequested()
        {
            requestReplayLevel?.Invoke();
        }

        private void HandleReturnToLevelMenuRequested()
        {
            requestReturnToLevelMenu?.Invoke();
        }
    }
}
