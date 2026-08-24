namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Opaque identity returned after a level scene and its child scope are ready.
    /// </summary>
    public readonly struct LevelSceneHandle
    {
        public LevelSceneHandle(int levelNumber, string scenePath, int scopeToken)
        {
            LevelNumber = levelNumber;
            ScenePath = scenePath;
            ScopeToken = scopeToken;
        }

        public int LevelNumber { get; }
        public string ScenePath { get; }
        public int ScopeToken { get; }
        public bool IsValid => LevelNumber > 0 && !string.IsNullOrEmpty(ScenePath) && ScopeToken != 0;
    }
}
