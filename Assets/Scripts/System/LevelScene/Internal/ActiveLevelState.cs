namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Tracks the one active level handle and whether a scene transition is in progress.
    /// </summary>
    internal sealed class ActiveLevelState
    {
        public LevelSceneHandle Handle { get; private set; }
        public bool HasActiveLevel => Handle.IsValid;
        public bool IsTransitioning { get; private set; }

        public void BeginTransition()
        {
            IsTransitioning = true;
        }

        public void EndTransition()
        {
            IsTransitioning = false;
        }

        public void Set(LevelSceneHandle handle)
        {
            Handle = handle;
        }

        public void Clear()
        {
            Handle = default;
        }
    }
}
