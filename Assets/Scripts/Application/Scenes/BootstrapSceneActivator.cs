using UnityEngine.SceneManagement;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Restores the persistent Bootstrap scene as Unity's active scene.
    /// </summary>
    internal sealed class BootstrapSceneActivator
    {
        public const string ScenePath = "Assets/Scenes/Bootstrap.unity";

        public bool TryActivate(out string error)
        {
            Scene bootstrapScene = SceneManager.GetSceneByPath(ScenePath);
            if (!bootstrapScene.IsValid() || !bootstrapScene.isLoaded)
            {
                error = $"Bootstrap scene '{ScenePath}' is not loaded.";
                return false;
            }

            if (SceneManager.GetActiveScene() == bootstrapScene
                || SceneManager.SetActiveScene(bootstrapScene))
            {
                error = string.Empty;
                return true;
            }

            error = $"Bootstrap scene '{ScenePath}' could not become active.";
            return false;
        }
    }
}
