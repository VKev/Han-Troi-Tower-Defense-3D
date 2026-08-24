namespace TowerDefense3D.GameFlow
{
    public enum LevelTransitionStatus
    {
        Success,
        Busy,
        InvalidLevel,
        SceneNotInBuild,
        UnloadFailed,
        LoadFailed,
        ActivationFailed,
        MissingScope,
        MultipleScopes,
        InitializationFailed,
        ScopeMismatch
    }

    public readonly struct LevelTransitionResult
    {
        public LevelTransitionResult(LevelTransitionStatus status, int levelNumber, string error)
        {
            Status = status;
            LevelNumber = levelNumber;
            Error = error ?? string.Empty;
        }

        public LevelTransitionStatus Status { get; }
        public int LevelNumber { get; }
        public string Error { get; }
        public bool IsSuccess => Status == LevelTransitionStatus.Success;
    }
}
