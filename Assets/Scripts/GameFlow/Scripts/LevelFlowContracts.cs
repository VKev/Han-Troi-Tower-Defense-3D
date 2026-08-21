using System;
using TowerDefense3D.Towers;

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
        MissingContext,
        MultipleContexts,
        InitializationFailed,
        Cancelled
    }

    public readonly struct LevelLoadRequest
    {
        public LevelLoadRequest(int levelNumber, string scenePath)
        {
            LevelNumber = levelNumber;
            ScenePath = scenePath ?? string.Empty;
        }

        public int LevelNumber { get; }
        public string ScenePath { get; }

        public bool IsValid =>
            LevelNumber > 0
            && ScenePath.StartsWith("Assets/", StringComparison.Ordinal)
            && ScenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);
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

    public readonly struct LevelSceneRuntimeContext
    {
        public LevelSceneRuntimeContext(
            int levelNumber,
            Action requestReturnToMenu,
            TowerNetworkManager towerNetworkManager = null)
        {
            LevelNumber = levelNumber;
            RequestReturnToMenu = requestReturnToMenu;
            TowerNetworkManager = towerNetworkManager;
        }

        public int LevelNumber { get; }
        public Action RequestReturnToMenu { get; }
        public TowerNetworkManager TowerNetworkManager { get; }
        public bool IsValid => LevelNumber > 0 && RequestReturnToMenu != null;
    }

    public interface ILevelSceneParticipant
    {
        void Initialize(LevelSceneRuntimeContext context);
        void Shutdown();
    }
}
