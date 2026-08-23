using System;
using System.Collections;
using System.Collections.Generic;
using TowerDefense3D.Towers;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Loads a level scene additively and hands it off to its LevelSceneContext.
    /// </summary>
    internal sealed class LevelLoadSequence
    {
        private const string LevelLifetimeScopeTypeName =
            "TowerDefense3D.GameFlow.LevelLifetimeScope";

        private readonly ActiveLevelState activeLevelState;
        private readonly LevelUnloadSequence unloadSequence;
        private readonly BootstrapSceneActivator bootstrapActivator;
        private readonly LifetimeScope applicationScope;

        public LevelLoadSequence(
            ActiveLevelState activeLevelState,
            LevelUnloadSequence unloadSequence,
            BootstrapSceneActivator bootstrapActivator,
            LifetimeScope applicationScope)
        {
            this.activeLevelState = activeLevelState;
            this.unloadSequence = unloadSequence;
            this.bootstrapActivator = bootstrapActivator;
            this.applicationScope = applicationScope;
        }

        public IEnumerator Run(
            LevelLoadRequest request,
            TowerNetworkManager towerNetworkManager,
            Action requestReturnToMenu,
            Action<LevelTransitionResult> completion)
        {
            LevelTransitionResult Fail(LevelTransitionStatus status, string message) =>
                new LevelTransitionResult(status, request.LevelNumber, message);

            if (!Application.CanStreamedLevelBeLoaded(request.ScenePath))
            {
                completion(Fail(
                    LevelTransitionStatus.SceneNotInBuild,
                    $"Scene '{request.ScenePath}' is not enabled in the active player scene list."));
                yield break;
            }

            LevelTransitionResult unloadResult = default;
            yield return unloadSequence.Run(result => unloadResult = result);
            if (!unloadResult.IsSuccess)
            {
                completion(unloadResult);
                yield break;
            }

            AsyncOperation loadOperation;
            try
            {
                loadOperation = SceneManager.LoadSceneAsync(
                    request.ScenePath,
                    LoadSceneMode.Additive);
            }
            catch (Exception exception)
            {
                completion(Fail(LevelTransitionStatus.LoadFailed, exception.Message));
                yield break;
            }

            if (loadOperation == null)
            {
                completion(Fail(
                    LevelTransitionStatus.LoadFailed,
                    $"Unity did not start loading '{request.ScenePath}'."));
                yield break;
            }

            yield return loadOperation;

            Scene loadedScene = SceneManager.GetSceneByPath(request.ScenePath);
            if (!loadedScene.IsValid() || !loadedScene.isLoaded)
            {
                completion(Fail(
                    LevelTransitionStatus.LoadFailed,
                    $"Scene '{request.ScenePath}' did not become loaded."));
                yield break;
            }

            if (!SceneManager.SetActiveScene(loadedScene))
            {
                yield return bootstrapActivator.CleanupFailedTarget(loadedScene);
                completion(Fail(
                    LevelTransitionStatus.ActivationFailed,
                    $"Scene '{request.ScenePath}' could not become active."));
                yield break;
            }

            List<LevelSceneContext> contexts = FindLevelContexts(loadedScene);
            if (contexts.Count != 1)
            {
                LevelTransitionStatus status = contexts.Count == 0
                    ? LevelTransitionStatus.MissingContext
                    : LevelTransitionStatus.MultipleContexts;
                yield return bootstrapActivator.CleanupFailedTarget(loadedScene);
                completion(Fail(
                    status,
                    $"Scene '{request.ScenePath}' requires exactly one LevelSceneContext; found {contexts.Count}."));
                yield break;
            }

            LevelSceneContext context = contexts[0];
            List<LifetimeScope> lifetimeScopes = FindLevelLifetimeScopes(loadedScene);
            if (lifetimeScopes.Count != 1)
            {
                yield return bootstrapActivator.CleanupFailedTarget(loadedScene);
                completion(Fail(
                    LevelTransitionStatus.InitializationFailed,
                    $"Scene '{request.ScenePath}' requires exactly one LevelLifetimeScope; "
                    + $"found {lifetimeScopes.Count}."));
                yield break;
            }

            string lifetimeScopeError = null;
            try
            {
                using (LifetimeScope.EnqueueParent(applicationScope))
                {
                    lifetimeScopes[0].Build();
                }
            }
            catch (Exception exception)
            {
                lifetimeScopeError = exception.Message;
            }

            if (lifetimeScopeError != null)
            {
                yield return bootstrapActivator.CleanupFailedTarget(loadedScene);
                completion(Fail(LevelTransitionStatus.InitializationFailed, lifetimeScopeError));
                yield break;
            }

            var runtimeContext = new LevelSceneRuntimeContext(
                request.LevelNumber,
                requestReturnToMenu,
                towerNetworkManager);
            if (!context.TryInitialize(runtimeContext, out string initializationError))
            {
                yield return bootstrapActivator.CleanupFailedTarget(loadedScene);
                completion(Fail(LevelTransitionStatus.InitializationFailed, initializationError));
                yield break;
            }

            activeLevelState.Set(loadedScene, context);
            completion(Fail(LevelTransitionStatus.Success, string.Empty));
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

        private static List<LifetimeScope> FindLevelLifetimeScopes(Scene scene)
        {
            var lifetimeScopes = new List<LifetimeScope>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                LifetimeScope[] rootScopes =
                    roots[rootIndex].GetComponentsInChildren<LifetimeScope>(true);
                for (int scopeIndex = 0; scopeIndex < rootScopes.Length; scopeIndex++)
                {
                    LifetimeScope scope = rootScopes[scopeIndex];
                    if (string.Equals(
                            scope.GetType().FullName,
                            LevelLifetimeScopeTypeName,
                            StringComparison.Ordinal))
                    {
                        lifetimeScopes.Add(scope);
                    }
                }
            }

            return lifetimeScopes;
        }
    }
}
