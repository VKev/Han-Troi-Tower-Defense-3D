namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Owns level load, return-to-menu, and transition-completion callbacks.
    /// </summary>
    public sealed class LevelTransitionFlow
    {
        private readonly LevelSceneSystem levelSceneSystem;
        private readonly ApplicationUISystem applicationUiSystem;

        private GameFlowSystem gameFlowSystem;

        public LevelTransitionFlow(
            LevelSceneSystem levelSceneSystem,
            ApplicationUISystem applicationUiSystem)
        {
            this.levelSceneSystem = levelSceneSystem;
            this.applicationUiSystem = applicationUiSystem;
        }

        public void Initialize(GameFlowSystem system)
        {
            gameFlowSystem = system;
        }

        public void Shutdown()
        {
            gameFlowSystem = null;
        }

        /// <summary>
        /// Loads a level behind a fade to black rather than behind a loading panel.
        /// </summary>
        /// <remarks>
        /// The order matters. The screen is covered first and the load only started once it is
        /// fully black, because the scene appears the instant the load finishes: begun together, a
        /// fast load would land while the curtain was still half transparent and the swap would
        /// show through it. The menu is torn down after the cover is up for the same reason -
        /// taken down first, the player would watch it vanish.
        ///
        /// This is the transition that goes level to level as well, since that routes through here
        /// too. Returning to the menu does not: it has nothing to load worth hiding, and keeps its
        /// loading panel.
        /// </remarks>
        public void BeginLevelLoad(LevelLoadRequest request)
        {
            gameFlowSystem.SetState(GameFlowState.LoadingLevel);
            applicationUiSystem.SetInputBlocked(true);
            applicationUiSystem.CoverScreen(() =>
            {
                if (gameFlowSystem == null)
                {
                    return;
                }

                applicationUiSystem.HideBlockingError();
                applicationUiSystem.HideLevelMenu();
                levelSceneSystem.LoadLevel(request, result => OnLevelLoadCompleted(request, result));
            });
        }

        /// <summary>
        /// Leaves a level behind the same fade that entering one uses.
        /// </summary>
        /// <remarks>
        /// It used to put a loading panel up instead. Going in and coming out are the same journey
        /// in opposite directions, and having them look different made leaving feel like an error
        /// state rather than a move.
        ///
        /// Same ordering as the way in, and for the same reason: the unload only starts once the
        /// screen is fully black, because the level disappears the instant it completes. Started
        /// together, a fast unload would empty the screen while the curtain was still see-through.
        /// </remarks>
        public void BeginReturnToLevelMenu()
        {
            gameFlowSystem.SetState(GameFlowState.LoadingLevel);
            applicationUiSystem.SetInputBlocked(true);
            applicationUiSystem.CoverScreen(() =>
            {
                if (gameFlowSystem == null)
                {
                    return;
                }

                levelSceneSystem.UnloadActiveLevel(OnReturnToMenuCompleted);
            });
        }

        private void OnLevelLoadCompleted(LevelLoadRequest request, LevelTransitionResult result)
        {
            if (gameFlowSystem == null)
            {
                return;
            }

            if (result.IsSuccess)
            {
                gameFlowSystem.SetState(GameFlowState.Gameplay);
                applicationUiSystem.UncoverScreen(null);
                applicationUiSystem.SetInputBlocked(false);
                return;
            }

            // The error has to be readable, so the curtain lifts even though the load failed.
            gameFlowSystem.SetState(GameFlowState.BlockingError);
            applicationUiSystem.UncoverScreen(null);
            applicationUiSystem.SetInputBlocked(false);
            applicationUiSystem.ShowBlockingError(CreateTransitionErrorMessage(result),
                () => BeginLevelLoad(request), null);
        }

        private void OnReturnToMenuCompleted(LevelTransitionResult result)
        {
            if (gameFlowSystem == null)
            {
                return;
            }

            applicationUiSystem.HideLoading();
            if (result.IsSuccess)
            {
                // The menu is raised while the screen is still black, then uncovered - so the
                // player never watches it assemble itself.
                gameFlowSystem.ShowLevelMenu();
                applicationUiSystem.UncoverScreen(null);
                return;
            }

            // The curtain lifts even on failure, or the error would be announced to a black
            // screen that nobody can read.
            gameFlowSystem.SetState(GameFlowState.BlockingError);
            applicationUiSystem.UncoverScreen(null);
            applicationUiSystem.SetInputBlocked(false);
            applicationUiSystem.ShowBlockingError(CreateTransitionErrorMessage(result),
                BeginReturnToLevelMenu, null);
        }

        private static string CreateTransitionErrorMessage(LevelTransitionResult result)
        {
            return string.IsNullOrWhiteSpace(result.Error)
                ? $"Level transition failed with status {result.Status}."
                : result.Error;
        }
    }
}
