using System;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Unity scene-operation boundary consumed by the plain C# level-scene system.
    /// </summary>
    public interface ILevelSceneGateway
    {
        void LoadLevel(
            LevelLoadRequest request,
            Action<LevelSceneHandle, LevelTransitionResult> completion);

        void UnloadLevel(
            LevelSceneHandle handle,
            Action<LevelTransitionResult> completion);
    }
}
