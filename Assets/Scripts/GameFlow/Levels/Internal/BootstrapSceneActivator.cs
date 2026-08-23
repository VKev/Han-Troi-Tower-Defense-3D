using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Ensures the fallback bootstrap scene is active whenever no level scene owns the game.
    /// </summary>
    internal sealed class BootstrapSceneActivator
    {
        private readonly string bootstrapScenePath;

        public BootstrapSceneActivator(string bootstrapScenePath)
        {
            this.bootstrapScenePath = bootstrapScenePath;
        }

        public bool TryActivate(out string error)
        {
            Scene bootstrap = SceneManager.GetSceneByPath(bootstrapScenePath);
            if (SceneManager.GetActiveScene() == bootstrap)
            {
                error = string.Empty;
                return true;
            }

            if (!SceneManager.SetActiveScene(bootstrap))
            {
                error = $"Bootstrap scene '{bootstrapScenePath}' could not become active.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public IEnumerator CleanupFailedTarget(Scene scene)
        {
            AsyncOperation cleanupOperation = SceneManager.UnloadSceneAsync(scene);
            if (cleanupOperation != null)
            {
                yield return cleanupOperation;
            }

            TryActivate(out _);
        }
    }
}
