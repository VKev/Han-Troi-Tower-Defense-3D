using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Sole owner of native additive level-scene load and unload operations.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelSceneLoader : MonoBehaviour
    {
        private enum TransitionKind
        {
            None,
            Load,
            ReturnToMenu
        }

        [SerializeField] private string bootstrapScenePath = "Assets/Scenes/Bootstrap.unity";

        private readonly List<Action<LevelTransitionResult>> completionWaiters =
            new List<Action<LevelTransitionResult>>();

        private TransitionKind transitionKind;
        private int transitionLevelNumber;
        private string transitionScenePath = string.Empty;
        private Scene activeLevelScene;
        private LevelSceneContext activeLevelContext;

        public bool IsTransitioning => transitionKind != TransitionKind.None;
        public bool HasActiveLevel => activeLevelScene.IsValid() && activeLevelScene.isLoaded;
        public int ActiveLevelNumber => activeLevelContext != null ? activeLevelContext.LevelNumber : 0;
        public string ActiveScenePath => HasActiveLevel ? activeLevelScene.path : string.Empty;

        public void LoadLevel(
            LevelLoadRequest request,
            Action requestReturnToMenu,
            Action<LevelTransitionResult> completion)
        {
            if (!request.IsValid || requestReturnToMenu == null)
            {
                completion?.Invoke(new LevelTransitionResult(
                    LevelTransitionStatus.InvalidLevel,
                    request.LevelNumber,
                    "Level load request is invalid."));
                return;
            }

            if (IsTransitioning)
            {
                if (transitionKind == TransitionKind.Load
                    && transitionLevelNumber == request.LevelNumber
                    && string.Equals(transitionScenePath, request.ScenePath, StringComparison.Ordinal))
                {
                    AddCompletion(completion);
                }
                else
                {
                    completion?.Invoke(CreateBusyResult(request.LevelNumber));
                }

                return;
            }

            BeginTransition(TransitionKind.Load, request.LevelNumber, request.ScenePath, completion);
            StartCoroutine(LoadLevelRoutine(request, requestReturnToMenu));
        }

        public void UnloadActiveLevel(Action<LevelTransitionResult> completion)
        {
            if (IsTransitioning)
            {
                if (transitionKind == TransitionKind.ReturnToMenu)
                {
                    AddCompletion(completion);
                }
                else
                {
                    completion?.Invoke(CreateBusyResult(ActiveLevelNumber));
                }

                return;
            }

            BeginTransition(TransitionKind.ReturnToMenu, ActiveLevelNumber, ActiveScenePath, completion);
            StartCoroutine(UnloadForMenuRoutine());
        }

        private IEnumerator LoadLevelRoutine(LevelLoadRequest request, Action requestReturnToMenu)
        {
            if (!Application.CanStreamedLevelBeLoaded(request.ScenePath))
            {
                CompleteTransition(new LevelTransitionResult(
                    LevelTransitionStatus.SceneNotInBuild,
                    request.LevelNumber,
                    $"Scene '{request.ScenePath}' is not enabled in the active player scene list."));
                yield break;
            }

            Scene unexpectedExistingScene = SceneManager.GetSceneByPath(request.ScenePath);
            if (unexpectedExistingScene.IsValid() && unexpectedExistingScene.isLoaded && unexpectedExistingScene != activeLevelScene)
            {
                CompleteTransition(new LevelTransitionResult(
                    LevelTransitionStatus.LoadFailed,
                    request.LevelNumber,
                    $"Scene '{request.ScenePath}' is already loaded outside LevelSceneLoader ownership."));
                yield break;
            }

            LevelTransitionResult unloadResult = default;
            yield return UnloadActiveLevelInternal(result => unloadResult = result, true);
            if (!unloadResult.IsSuccess)
            {
                CompleteTransition(unloadResult);
                yield break;
            }

            AsyncOperation loadOperation;
            try
            {
                loadOperation = SceneManager.LoadSceneAsync(request.ScenePath, LoadSceneMode.Additive);
            }
            catch (Exception exception)
            {
                CompleteTransition(new LevelTransitionResult(
                    LevelTransitionStatus.LoadFailed,
                    request.LevelNumber,
                    exception.Message));
                yield break;
            }

            if (loadOperation == null)
            {
                CompleteTransition(new LevelTransitionResult(
                    LevelTransitionStatus.LoadFailed,
                    request.LevelNumber,
                    $"Unity did not start loading '{request.ScenePath}'."));
                yield break;
            }

            yield return loadOperation;

            Scene loadedScene = SceneManager.GetSceneByPath(request.ScenePath);
            if (!loadedScene.IsValid() || !loadedScene.isLoaded)
            {
                CompleteTransition(new LevelTransitionResult(
                    LevelTransitionStatus.LoadFailed,
                    request.LevelNumber,
                    $"Scene '{request.ScenePath}' did not become loaded."));
                yield break;
            }

            if (!SceneManager.SetActiveScene(loadedScene))
            {
                yield return CleanupFailedTarget(loadedScene);
                CompleteTransition(new LevelTransitionResult(
                    LevelTransitionStatus.ActivationFailed,
                    request.LevelNumber,
                    $"Scene '{request.ScenePath}' could not become active."));
                yield break;
            }

            List<LevelSceneContext> contexts = FindLevelContexts(loadedScene);
            if (contexts.Count != 1)
            {
                LevelTransitionStatus status = contexts.Count == 0
                    ? LevelTransitionStatus.MissingContext
                    : LevelTransitionStatus.MultipleContexts;
                yield return CleanupFailedTarget(loadedScene);
                CompleteTransition(new LevelTransitionResult(
                    status,
                    request.LevelNumber,
                    $"Scene '{request.ScenePath}' requires exactly one LevelSceneContext; found {contexts.Count}."));
                yield break;
            }

            LevelSceneContext context = contexts[0];
            var runtimeContext = new LevelSceneRuntimeContext(request.LevelNumber, requestReturnToMenu);
            if (!context.TryInitialize(runtimeContext, out string initializationError))
            {
                yield return CleanupFailedTarget(loadedScene);
                CompleteTransition(new LevelTransitionResult(
                    LevelTransitionStatus.InitializationFailed,
                    request.LevelNumber,
                    initializationError));
                yield break;
            }

            activeLevelScene = loadedScene;
            activeLevelContext = context;
            CompleteTransition(new LevelTransitionResult(
                LevelTransitionStatus.Success,
                request.LevelNumber,
                string.Empty));
        }

        private IEnumerator UnloadForMenuRoutine()
        {
            LevelTransitionResult result = default;
            yield return UnloadActiveLevelInternal(value => result = value, true);
            CompleteTransition(result);
        }

        private IEnumerator UnloadActiveLevelInternal(
            Action<LevelTransitionResult> completion,
            bool activateBootstrap)
        {
            int unloadingLevelNumber = ActiveLevelNumber;
            if (!HasActiveLevel)
            {
                ClearActiveLevelOwnership();
                if (activateBootstrap && !TryActivateBootstrap(out string activationError))
                {
                    completion(new LevelTransitionResult(
                        LevelTransitionStatus.ActivationFailed,
                        unloadingLevelNumber,
                        activationError));
                    yield break;
                }

                completion(new LevelTransitionResult(
                    LevelTransitionStatus.Success,
                    unloadingLevelNumber,
                    string.Empty));
                yield break;
            }

            activeLevelContext?.Shutdown();
            Scene sceneToUnload = activeLevelScene;
            AsyncOperation unloadOperation;
            try
            {
                unloadOperation = SceneManager.UnloadSceneAsync(sceneToUnload);
            }
            catch (Exception exception)
            {
                completion(new LevelTransitionResult(
                    LevelTransitionStatus.UnloadFailed,
                    unloadingLevelNumber,
                    exception.Message));
                yield break;
            }

            if (unloadOperation == null)
            {
                completion(new LevelTransitionResult(
                    LevelTransitionStatus.UnloadFailed,
                    unloadingLevelNumber,
                    $"Unity did not start unloading '{sceneToUnload.path}'."));
                yield break;
            }

            yield return unloadOperation;
            if (sceneToUnload.isLoaded)
            {
                completion(new LevelTransitionResult(
                    LevelTransitionStatus.UnloadFailed,
                    unloadingLevelNumber,
                    $"Scene '{sceneToUnload.path}' remained loaded."));
                yield break;
            }

            ClearActiveLevelOwnership();
            if (activateBootstrap && !TryActivateBootstrap(out string bootstrapError))
            {
                completion(new LevelTransitionResult(
                    LevelTransitionStatus.ActivationFailed,
                    unloadingLevelNumber,
                    bootstrapError));
                yield break;
            }

            completion(new LevelTransitionResult(
                LevelTransitionStatus.Success,
                unloadingLevelNumber,
                string.Empty));
        }

        private IEnumerator CleanupFailedTarget(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                yield break;
            }

            AsyncOperation cleanupOperation = SceneManager.UnloadSceneAsync(scene);
            if (cleanupOperation != null)
            {
                yield return cleanupOperation;
            }

            TryActivateBootstrap(out _);
        }

        private bool TryActivateBootstrap(out string error)
        {
            Scene bootstrap = SceneManager.GetSceneByPath(bootstrapScenePath);
            if (!bootstrap.IsValid() || !bootstrap.isLoaded)
            {
                error = $"Bootstrap scene '{bootstrapScenePath}' is not loaded.";
                return false;
            }

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

        private static List<LevelSceneContext> FindLevelContexts(Scene scene)
        {
            var contexts = new List<LevelSceneContext>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                LevelSceneContext[] rootContexts = roots[rootIndex].GetComponentsInChildren<LevelSceneContext>(true);
                contexts.AddRange(rootContexts);
            }

            return contexts;
        }

        private void BeginTransition(
            TransitionKind kind,
            int levelNumber,
            string scenePath,
            Action<LevelTransitionResult> completion)
        {
            transitionKind = kind;
            transitionLevelNumber = levelNumber;
            transitionScenePath = scenePath ?? string.Empty;
            completionWaiters.Clear();
            AddCompletion(completion);
        }

        private void AddCompletion(Action<LevelTransitionResult> completion)
        {
            if (completion != null)
            {
                completionWaiters.Add(completion);
            }
        }

        private void CompleteTransition(LevelTransitionResult result)
        {
            Action<LevelTransitionResult>[] waiters = completionWaiters.ToArray();
            completionWaiters.Clear();
            transitionKind = TransitionKind.None;
            transitionLevelNumber = 0;
            transitionScenePath = string.Empty;

            for (int index = 0; index < waiters.Length; index++)
            {
                try
                {
                    waiters[index](result);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }

        private void ClearActiveLevelOwnership()
        {
            activeLevelContext = null;
            activeLevelScene = default;
        }

        private static LevelTransitionResult CreateBusyResult(int levelNumber)
        {
            return new LevelTransitionResult(
                LevelTransitionStatus.Busy,
                levelNumber,
                "Another level transition is already running.");
        }

        private void OnDestroy()
        {
            if (IsTransitioning)
            {
                CompleteTransition(new LevelTransitionResult(
                    LevelTransitionStatus.Cancelled,
                    transitionLevelNumber,
                    "LevelSceneLoader was destroyed during a transition."));
            }
        }
    }
}
