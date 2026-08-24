using System;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Detaches and unloads the current level before clearing its active handle.
    /// </summary>
    internal sealed class LevelUnloadSequence
    {
        private readonly ActiveLevelState activeLevelState;
        private readonly ILevelSceneGateway gateway;

        public LevelUnloadSequence(
            ActiveLevelState activeLevelState,
            ILevelSceneGateway gateway)
        {
            this.activeLevelState = activeLevelState;
            this.gateway = gateway;
        }

        public void Run(Action<LevelTransitionResult> completion)
        {
            LevelSceneHandle handle = activeLevelState.Handle;
            gateway.UnloadLevel(
                handle,
                result =>
                {
                    if (result.Status != LevelTransitionStatus.UnloadFailed)
                    {
                        activeLevelState.Clear();
                    }

                    completion(result);
                });
        }
    }
}
