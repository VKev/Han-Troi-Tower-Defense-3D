using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Owns native additive scene operations and parented LevelLifetimeScope construction.
    /// </summary>
    internal sealed class VContainerLevelSceneGateway : ILevelSceneGateway
    {
        private readonly LifetimeScope applicationScope;
        private readonly BootstrapSceneActivator bootstrapSceneActivator;

        private Scene activeScene;
        private LevelLifetimeScope activeScope;
        private int activeScopeToken;

        public VContainerLevelSceneGateway(
            LifetimeScope applicationScope,
            BootstrapSceneActivator bootstrapSceneActivator)
        {
            this.applicationScope = applicationScope;
            this.bootstrapSceneActivator = bootstrapSceneActivator;
        }

        public void LoadLevel(
            LevelLoadRequest request,
            Action<LevelSceneHandle, LevelTransitionResult> completion)
        {
            if (!Application.CanStreamedLevelBeLoaded(request.ScenePath))
            {
                completion(
                    default,
                    CreateResult(
                        LevelTransitionStatus.SceneNotInBuild,
                        request.LevelNumber,
                        $"Scene '{request.ScenePath}' is not enabled in the active player scene list."));
                return;
            }

            AsyncOperation loadOperation;
            try
            {
                loadOperation = SceneManager.LoadSceneAsync(request.ScenePath, LoadSceneMode.Additive);
            }
            catch (Exception exception)
            {
                completion(
                    default,
                    CreateResult(LevelTransitionStatus.LoadFailed, request.LevelNumber, exception.Message));
                return;
            }

            if (loadOperation == null)
            {
                completion(
                    default,
                    CreateResult(
                        LevelTransitionStatus.LoadFailed,
                        request.LevelNumber,
                        $"Unity did not start loading '{request.ScenePath}'."));
                return;
            }

            loadOperation.completed += _ => CompleteLoad(request, completion);
        }

        public void UnloadLevel(
            LevelSceneHandle handle,
            Action<LevelTransitionResult> completion)
        {
            if (!handle.IsValid)
            {
                CompleteBootstrapActivation(0, completion);
                return;
            }

            if (handle.ScopeToken != activeScopeToken
                || !activeScene.IsValid()
                || activeScene.path != handle.ScenePath)
            {
                completion(CreateResult(
                    LevelTransitionStatus.UnloadFailed,
                    handle.LevelNumber,
                    "The active Unity level scope does not match the requested unload handle."));
                return;
            }

            activeScope.ReleaseLevelSystems();
            Scene sceneToUnload = activeScene;
            AsyncOperation unloadOperation;
            try
            {
                unloadOperation = SceneManager.UnloadSceneAsync(sceneToUnload);
            }
            catch (Exception exception)
            {
                completion(CreateResult(LevelTransitionStatus.UnloadFailed, handle.LevelNumber, exception.Message));
                return;
            }

            if (unloadOperation == null)
            {
                completion(CreateResult(
                    LevelTransitionStatus.UnloadFailed,
                    handle.LevelNumber,
                    $"Unity did not start unloading '{handle.ScenePath}'."));
                return;
            }

            unloadOperation.completed += _ => CompleteUnload(handle, sceneToUnload, completion);
        }

        private void CompleteLoad(
            LevelLoadRequest request,
            Action<LevelSceneHandle, LevelTransitionResult> completion)
        {
            Scene loadedScene = SceneManager.GetSceneByPath(request.ScenePath);
            if (!loadedScene.IsValid() || !loadedScene.isLoaded)
            {
                completion(
                    default,
                    CreateResult(
                        LevelTransitionStatus.LoadFailed,
                        request.LevelNumber,
                        $"Scene '{request.ScenePath}' did not become loaded."));
                return;
            }

            List<LevelLifetimeScope> scopes = FindLevelScopes(loadedScene);
            if (scopes.Count != 1)
            {
                LevelTransitionStatus status = scopes.Count == 0
                    ? LevelTransitionStatus.MissingScope
                    : LevelTransitionStatus.MultipleScopes;
                CleanupFailedScene(
                    loadedScene,
                    null,
                    CreateResult(
                        status,
                        request.LevelNumber,
                        $"Scene '{request.ScenePath}' requires exactly one LevelLifetimeScope; found {scopes.Count}."),
                    completion);
                return;
            }

            LevelLifetimeScope scope = scopes[0];
            if (scope.LevelNumber != request.LevelNumber)
            {
                CleanupFailedScene(
                    loadedScene,
                    scope,
                    CreateResult(
                        LevelTransitionStatus.ScopeMismatch,
                        request.LevelNumber,
                        $"Loaded level {request.LevelNumber} does not match authored scope {scope.LevelNumber}."),
                    completion);
                return;
            }

            if (scope.autoRun || scope.Container != null)
            {
                CleanupFailedScene(
                    loadedScene,
                    scope,
                    CreateResult(
                        LevelTransitionStatus.InitializationFailed,
                        request.LevelNumber,
                        "LevelLifetimeScope must disable Auto Run so the gateway can assign its application parent."),
                    completion);
                return;
            }

            if (!SceneManager.SetActiveScene(loadedScene))
            {
                CleanupFailedScene(
                    loadedScene,
                    scope,
                    CreateResult(
                        LevelTransitionStatus.ActivationFailed,
                        request.LevelNumber,
                        $"Scene '{request.ScenePath}' could not become active."),
                    completion);
                return;
            }

            try
            {
                using (LifetimeScope.EnqueueParent(applicationScope))
                {
                    scope.Build();
                }
            }
            catch (Exception exception)
            {
                CleanupFailedScene(
                    loadedScene,
                    scope,
                    CreateResult(LevelTransitionStatus.InitializationFailed, request.LevelNumber, exception.Message),
                    completion);
                return;
            }

            activeScene = loadedScene;
            activeScope = scope;
            activeScopeToken = scope.GetInstanceID();
            var handle = new LevelSceneHandle(request.LevelNumber, request.ScenePath, activeScopeToken);
            completion(handle, CreateResult(LevelTransitionStatus.Success, request.LevelNumber, string.Empty));
        }

        private void CompleteUnload(
            LevelSceneHandle handle,
            Scene unloadedScene,
            Action<LevelTransitionResult> completion)
        {
            if (unloadedScene.isLoaded)
            {
                completion(CreateResult(
                    LevelTransitionStatus.UnloadFailed,
                    handle.LevelNumber,
                    $"Scene '{handle.ScenePath}' remained loaded."));
                return;
            }

            activeScene = default;
            activeScope = null;
            activeScopeToken = 0;
            CompleteBootstrapActivation(handle.LevelNumber, completion);
        }

        private void CleanupFailedScene(
            Scene scene,
            LevelLifetimeScope scope,
            LevelTransitionResult result,
            Action<LevelSceneHandle, LevelTransitionResult> completion)
        {
            scope?.ReleaseLevelSystems();
            AsyncOperation cleanupOperation;
            try
            {
                cleanupOperation = SceneManager.UnloadSceneAsync(scene);
            }
            catch
            {
                bootstrapSceneActivator.TryActivate(out _);
                completion(default, result);
                return;
            }

            if (cleanupOperation == null)
            {
                bootstrapSceneActivator.TryActivate(out _);
                completion(default, result);
                return;
            }

            cleanupOperation.completed += ignoredOperation =>
            {
                bootstrapSceneActivator.TryActivate(out _);
                completion(default, result);
            };
        }

        private void CompleteBootstrapActivation(
            int levelNumber,
            Action<LevelTransitionResult> completion)
        {
            if (bootstrapSceneActivator.TryActivate(out string error))
            {
                completion(CreateResult(LevelTransitionStatus.Success, levelNumber, string.Empty));
                return;
            }

            completion(CreateResult(LevelTransitionStatus.ActivationFailed, levelNumber, error));
        }

        private static List<LevelLifetimeScope> FindLevelScopes(Scene scene)
        {
            var scopes = new List<LevelLifetimeScope>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                scopes.AddRange(roots[index].GetComponentsInChildren<LevelLifetimeScope>(true));
            }

            return scopes;
        }

        private static LevelTransitionResult CreateResult(
            LevelTransitionStatus status,
            int levelNumber,
            string error)
        {
            return new LevelTransitionResult(status, levelNumber, error);
        }
    }
}
