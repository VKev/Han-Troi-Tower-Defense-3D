using System;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Unloads the previous level before loading and recording the requested level scope.
    /// </summary>
    internal sealed class LevelLoadSequence
    {
        private readonly ActiveLevelState activeLevelState;
        private readonly LevelUnloadSequence unloadSequence;
        private readonly ILevelSceneGateway gateway;

        public LevelLoadSequence(
            ActiveLevelState activeLevelState,
            LevelUnloadSequence unloadSequence,
            ILevelSceneGateway gateway)
        {
            this.activeLevelState = activeLevelState;
            this.unloadSequence = unloadSequence;
            this.gateway = gateway;
        }

        public void Run(
            LevelLoadRequest request,
            Action<LevelTransitionResult> completion)
        {
            unloadSequence.Run(
                unloadResult =>
                {
                    if (!unloadResult.IsSuccess)
                    {
                        completion(unloadResult);
                        return;
                    }

                    gateway.LoadLevel(
                        request,
                        (handle, loadResult) =>
                        {
                            if (loadResult.IsSuccess)
                            {
                                activeLevelState.Set(handle);
                            }

                            completion(loadResult);
                        });
                });
        }
    }
}
