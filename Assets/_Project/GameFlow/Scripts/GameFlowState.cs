namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Mutually exclusive primary phases owned by the application flow coordinator.
    /// Save warnings are presented independently and are not a primary phase.
    /// </summary>
    public enum GameFlowState
    {
        Booting,
        LevelMenu,
        LoadingLevel,
        Gameplay,
        BlockingError
    }
}
