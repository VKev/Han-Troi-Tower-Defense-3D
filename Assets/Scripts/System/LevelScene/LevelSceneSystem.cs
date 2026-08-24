using System;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Serializes level load and unload commands behind one GameFlow-facing facade.
    /// </summary>
    public sealed class LevelSceneSystem
    {
        private readonly ActiveLevelState activeLevelState;
        private readonly LevelLoadSequence loadSequence;
        private readonly LevelUnloadSequence unloadSequence;

        public LevelSceneSystem(ILevelSceneGateway gateway)
        {
            activeLevelState = new ActiveLevelState();
            unloadSequence = new LevelUnloadSequence(activeLevelState, gateway);
            loadSequence = new LevelLoadSequence(activeLevelState, unloadSequence, gateway);
        }

        public bool HasActiveLevel => activeLevelState.HasActiveLevel;
        public bool IsTransitioning => activeLevelState.IsTransitioning;

        public void LoadLevel(
            LevelLoadRequest request,
            Action<LevelTransitionResult> completion)
        {
            if (activeLevelState.IsTransitioning)
            {
                completion(CreateResult(
                    LevelTransitionStatus.Busy,
                    request.LevelNumber,
                    "A level transition is in progress."));
                return;
            }

            if (!request.IsValid)
            {
                completion(CreateResult(
                    LevelTransitionStatus.InvalidLevel,
                    request.LevelNumber,
                    "Level requests require a positive number and a full Assets/.../*.unity scene path."));
                return;
            }

            activeLevelState.BeginTransition();
            loadSequence.Run(request, CompleteTransition(completion));
        }

        public void UnloadActiveLevel(Action<LevelTransitionResult> completion)
        {
            if (activeLevelState.IsTransitioning)
            {
                completion(CreateResult(
                    LevelTransitionStatus.Busy,
                    activeLevelState.Handle.LevelNumber,
                    "A level transition is in progress."));
                return;
            }

            activeLevelState.BeginTransition();
            unloadSequence.Run(CompleteTransition(completion));
        }

        private Action<LevelTransitionResult> CompleteTransition(
            Action<LevelTransitionResult> completion)
        {
            return result =>
            {
                activeLevelState.EndTransition();
                completion(result);
            };
        }

        private static LevelTransitionResult CreateResult(
            LevelTransitionStatus status,
            int levelNumber,
            string error)
        {
            return new LevelTransitionResult(status, levelNumber, error);
        }
    }
}
