using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Unloads the active level scene and reactivates the bootstrap scene.
    /// </summary>
    internal sealed class LevelUnloadSequence
    {
        private readonly ActiveLevelState activeLevelState;
        private readonly BootstrapSceneActivator bootstrapActivator;

        public LevelUnloadSequence(ActiveLevelState activeLevelState, BootstrapSceneActivator bootstrapActivator)
        {
            this.activeLevelState = activeLevelState;
            this.bootstrapActivator = bootstrapActivator;
        }

        public IEnumerator Run(Action<LevelTransitionResult> completion)
        {
            int unloadingLevelNumber = activeLevelState.LevelNumber;
            if (!activeLevelState.HasActiveLevel)
            {
                if (!bootstrapActivator.TryActivate(out string activationError))
                {
                    completion(new LevelTransitionResult(
                        LevelTransitionStatus.ActivationFailed,
                        unloadingLevelNumber,
                        activationError));
                    yield break;
                }

                completion(new LevelTransitionResult(
                    LevelTransitionStatus.Success,
                    unloadingLevelNumber,
                    string.Empty));
                yield break;
            }

            activeLevelState.Context.Shutdown();
            Scene sceneToUnload = activeLevelState.Scene;
            AsyncOperation unloadOperation;
            try
            {
                unloadOperation = SceneManager.UnloadSceneAsync(sceneToUnload);
            }
            catch (Exception exception)
            {
                completion(new LevelTransitionResult(
                    LevelTransitionStatus.UnloadFailed,
                    unloadingLevelNumber,
                    exception.Message));
                yield break;
            }

            if (unloadOperation == null)
            {
                completion(new LevelTransitionResult(
                    LevelTransitionStatus.UnloadFailed,
                    unloadingLevelNumber,
                    $"Unity did not start unloading '{sceneToUnload.path}'."));
                yield break;
            }

            yield return unloadOperation;
            if (sceneToUnload.isLoaded)
            {
                completion(new LevelTransitionResult(
                    LevelTransitionStatus.UnloadFailed,
                    unloadingLevelNumber,
                    $"Scene '{sceneToUnload.path}' remained loaded."));
                yield break;
            }

            activeLevelState.Clear();
            if (!bootstrapActivator.TryActivate(out string bootstrapError))
            {
                completion(new LevelTransitionResult(
                    LevelTransitionStatus.ActivationFailed,
                    unloadingLevelNumber,
                    bootstrapError));
                yield break;
            }

            completion(new LevelTransitionResult(
                LevelTransitionStatus.Success,
                unloadingLevelNumber,
                string.Empty));
        }
    }
}
