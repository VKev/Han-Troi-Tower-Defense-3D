using System;
using TowerDefense3D.Towers;
using UnityEngine;
using VContainer;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Sole owner of native additive level-scene load and unload operations.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelSceneLoader : MonoBehaviour
    {
        [SerializeField] private string bootstrapScenePath = "Assets/Scenes/Bootstrap.unity";

        private ActiveLevelState activeLevelState;
        private LevelUnloadSequence unloadSequence;
        private LevelLoadSequence loadSequence;

        public bool HasActiveLevel => activeLevelState.HasActiveLevel;
        public int ActiveLevelNumber => activeLevelState.LevelNumber;
        public string ActiveScenePath => activeLevelState.ScenePath;

        internal string BootstrapScenePath => bootstrapScenePath;

        [Inject]
        private void Construct(
            ActiveLevelState activeLevelState,
            LevelUnloadSequence unloadSequence,
            LevelLoadSequence loadSequence)
        {
            this.activeLevelState = activeLevelState;
            this.unloadSequence = unloadSequence;
            this.loadSequence = loadSequence;
        }

        public void LoadLevel(
            LevelLoadRequest request,
            TowerNetworkManager towerNetworkManager,
            Action requestReturnToMenu,
            Action<LevelTransitionResult> completion)
        {
            StartCoroutine(loadSequence.Run(request, towerNetworkManager, requestReturnToMenu, completion));
        }

        public void UnloadActiveLevel(Action<LevelTransitionResult> completion)
        {
            StartCoroutine(unloadSequence.Run(completion));
        }

    }
}
