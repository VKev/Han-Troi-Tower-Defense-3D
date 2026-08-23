using UnityEngine.SceneManagement;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Single source of truth for which level scene is currently loaded and active.
    /// </summary>
    internal sealed class ActiveLevelState
    {
        private Scene scene;
        private LevelSceneContext context;

        public bool HasActiveLevel => scene.IsValid() && scene.isLoaded;
        public int LevelNumber => HasActiveLevel ? context.LevelNumber : 0;
        public string ScenePath => HasActiveLevel ? scene.path : string.Empty;
        public Scene Scene => scene;
        public LevelSceneContext Context => context;

        public void Set(Scene loadedScene, LevelSceneContext loadedContext)
        {
            scene = loadedScene;
            context = loadedContext;
        }

        public void Clear()
        {
            scene = default;
            context = null;
        }
    }
}
