using System;
using TowerDefense3D.Core;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Coordinates focused application flows under ApplicationEntryPoint.
    /// </summary>
    public sealed class GameFlowSystem
    {
        private readonly ApplicationBootFlow applicationBootFlow;
        private readonly LevelMenuFlow levelMenuFlow;
        private readonly LevelTransitionFlow levelTransitionFlow;
        private readonly SaveRecoveryFlow saveRecoveryFlow;
        private readonly StateMachine<GameFlowState> stateMachine =
            new StateMachine<GameFlowState>(GameFlowState.Booting, CanTransition);

        private bool isStarted;

        public GameFlowState State => stateMachine.CurrentState;

        public GameFlowSystem(
            ApplicationBootFlow applicationBootFlow,
            LevelMenuFlow levelMenuFlow,
            LevelTransitionFlow levelTransitionFlow,
            SaveRecoveryFlow saveRecoveryFlow)
        {
            this.applicationBootFlow = applicationBootFlow;
            this.levelMenuFlow = levelMenuFlow;
            this.levelTransitionFlow = levelTransitionFlow;
            this.saveRecoveryFlow = saveRecoveryFlow;
        }

        public void Start()
        {
            try
            {
                applicationBootFlow.Initialize(this);
                levelMenuFlow.Initialize(this);
                levelTransitionFlow.Initialize(this);
                isStarted = true;
                applicationBootFlow.Boot();
            }
            catch (Exception startupException)
            {
                isStarted = false;
                try
                {
                    levelTransitionFlow.Shutdown();
                    levelMenuFlow.Shutdown();
                    applicationBootFlow.Shutdown();
                }
                catch (Exception rollbackException)
                {
                    throw new AggregateException(
                        "GameFlowSystem startup and rollback both failed.",
                        startupException,
                        rollbackException);
                }

                throw;
            }
        }

        public void Dispose()
        {
            if (!isStarted)
            {
                return;
            }

            isStarted = false;
            levelTransitionFlow.Shutdown();
            levelMenuFlow.Shutdown();
            applicationBootFlow.Shutdown();
        }

        internal void SetState(GameFlowState state)
        {
            stateMachine.TransitionTo(state);
        }

        internal void ShowLevelMenu()
        {
            levelMenuFlow.Show();
        }

        internal void BeginLevelLoad(LevelLoadRequest request)
        {
            if (State == GameFlowState.LevelMenu)
            {
                levelTransitionFlow.BeginLevelLoad(request);
            }
        }

        public void RequestReturnToLevelMenu()
        {
            if (State == GameFlowState.Gameplay)
            {
                levelTransitionFlow.BeginReturnToLevelMenu();
            }
        }

        internal void ShowSaveWarning(string error)
        {
            saveRecoveryFlow.ShowWarning(error);
        }

        private static bool CanTransition(GameFlowState currentState, GameFlowState nextState)
        {
            switch (currentState)
            {
                case GameFlowState.Booting:
                    return nextState == GameFlowState.LevelMenu
                        || nextState == GameFlowState.BlockingError;
                case GameFlowState.LevelMenu:
                    return nextState == GameFlowState.LoadingLevel;
                case GameFlowState.LoadingLevel:
                    return nextState == GameFlowState.Gameplay
                        || nextState == GameFlowState.LevelMenu
                        || nextState == GameFlowState.BlockingError;
                case GameFlowState.Gameplay:
                    return nextState == GameFlowState.LoadingLevel;
                case GameFlowState.BlockingError:
                    return nextState == GameFlowState.Booting
                        || nextState == GameFlowState.LevelMenu
                        || nextState == GameFlowState.LoadingLevel;
                default:
                    return false;
            }
        }
    }
}
