using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TowerDefense3D.GameplayInput;
using TowerDefense3D.GridPlacement;
using TowerDefense3D.Towers;

namespace TowerDefense3D.GameFlow.Editor
{
    public static class LevelCatalogValidator
    {
        public const string DefaultCatalogPath = "Assets/Config/GameFlow/LevelCatalog.asset";
        public const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";
        private const string ApplicationLifetimeScopeTypeName =
            "TowerDefense3D.GameFlow.ApplicationLifetimeScope";
        private const string LevelLifetimeScopeTypeName =
            "TowerDefense3D.GameFlow.LevelLifetimeScope";

        [MenuItem("Tools/Tower Defense/Validate Game Flow")]
        public static void ValidateFromMenu()
        {
            LevelCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(DefaultCatalogPath);
            List<string> errors = CollectErrors(catalog);
            if (errors.Count == 0)
            {
                Debug.Log("Game Flow validation passed.");
                return;
            }

            for (int index = 0; index < errors.Count; index++)
            {
                Debug.LogError(errors[index]);
            }
        }

        public static List<string> CollectErrors(LevelCatalog catalog)
        {
            var errors = new List<string>();
            if (catalog == null)
            {
                errors.Add($"Level Catalog is missing at '{DefaultCatalogPath}'.");
                return errors;
            }

            if (!catalog.TryValidate(out string catalogError))
            {
                errors.Add(catalogError);
                return errors;
            }

            EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
            var enabledBuildPaths = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < buildScenes.Length; index++)
            {
                EditorBuildSettingsScene buildScene = buildScenes[index];
                if (buildScene.enabled)
                {
                    enabledBuildPaths.Add(buildScene.path);
                }
            }

            if (buildScenes.Length == 0
                || !buildScenes[0].enabled
                || !string.Equals(buildScenes[0].path, BootstrapScenePath, StringComparison.Ordinal))
            {
                errors.Add($"The first enabled player scene must be '{BootstrapScenePath}'.");
            }

            if (!enabledBuildPaths.Contains(BootstrapScenePath))
            {
                errors.Add($"Bootstrap scene '{BootstrapScenePath}' is not enabled in the player scene list.");
            }

            ValidateBootstrap(errors);

            IReadOnlyList<LevelCatalogEntry> levels = catalog.Levels;
            for (int index = 0; index < levels.Count; index++)
            {
                LevelCatalogEntry entry = levels[index];
                if (!enabledBuildPaths.Contains(entry.ScenePath))
                {
                    errors.Add(
                        $"Level {entry.LevelNumber} scene '{entry.ScenePath}' "
                        + "is not enabled in the player scene list.");
                }

                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(entry.ScenePath) == null)
                {
                    errors.Add($"Level {entry.LevelNumber} scene is missing at '{entry.ScenePath}'.");
                    continue;
                }

                ValidateLevelScene(entry, errors);
            }

            return errors;
        }

        private static void ValidateBootstrap(List<string> errors)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapScenePath) == null)
            {
                errors.Add($"Bootstrap scene is missing at '{BootstrapScenePath}'.");
                return;
            }

            InspectScene(
                BootstrapScenePath,
                scene =>
                {
                    int lifetimeScopes = CountComponentsByFullName(
                        scene,
                        ApplicationLifetimeScopeTypeName);
                    if (lifetimeScopes != 1)
                    {
                        errors.Add(
                            $"Bootstrap requires exactly one ApplicationLifetimeScope; found {lifetimeScopes}.");
                    }

                    RequireExactlyOne<LevelSceneLoader>(scene, "LevelSceneLoader", errors);
                    RequireExactlyOne<ApplicationUIManager>(scene, "ApplicationUIManager", errors);
                    RequireExactlyOne<SafeAreaView>(scene, "SafeAreaView", errors);
                    RequireExactlyOne<EventSystem>(scene, "EventSystem", errors);

                    int levelContexts = CountComponents<LevelSceneContext>(scene);
                    if (levelContexts != 0)
                    {
                        errors.Add($"Bootstrap must not contain LevelSceneContext; found {levelContexts}.");
                    }

                    int levelLifetimeScopes = CountComponentsByFullName(scene, LevelLifetimeScopeTypeName);
                    if (levelLifetimeScopes != 0)
                    {
                        errors.Add(
                            $"Bootstrap must not contain LevelLifetimeScope; found {levelLifetimeScopes}.");
                    }
                },
                errors);
        }

        private static void ValidateLevelScene(LevelCatalogEntry entry, List<string> errors)
        {
            InspectScene(
                entry.ScenePath,
                scene =>
                {
                    RequireExactlyOne<LevelSceneContext>(
                        scene,
                        $"LevelSceneContext for Level {entry.LevelNumber}",
                        errors);
                    RequireExactlyOneByFullName(scene, LevelLifetimeScopeTypeName, "LevelLifetimeScope", errors);
                    ValidateLevelLifetimeScope(scene, entry.LevelNumber, errors);
                    RequireExactlyOne<BoardView>(scene, "BoardView", errors);
                    RequireExactlyOne<BoardCameraView>(scene, "BoardCameraView", errors);
                    RequireExactlyOne<GameplayInputSource>(scene, "GameplayInputSource", errors);
                    RequireExactlyOne<GridPlacementView>(scene, "GridPlacementView", errors);
                    RequireExactlyOne<TowerInstanceFactory>(scene, "TowerInstanceFactory", errors);
                    RequireExactlyOne<GridPlacementPresenter>(scene, "GridPlacementPresenter", errors);
                    RequireExactlyOne<TowerNetworkSceneAdapter>(scene, "TowerNetworkSceneAdapter", errors);
                    RequireExactlyOne<TowerLinkView>(scene, "TowerLinkView", errors);
                    RequireExactlyOne<TowerProjectilePoolView>(scene, "TowerProjectilePoolView", errors);
                    RequireExactlyOne<SafeAreaView>(scene, "SafeAreaView", errors);

                    int eventSystems = CountComponents<EventSystem>(scene);
                    if (eventSystems != 0)
                    {
                        errors.Add($"Level {entry.LevelNumber} must not contain EventSystem; found {eventSystems}.");
                    }

                    int lifetimeScopes = CountComponentsByFullName(
                        scene,
                        ApplicationLifetimeScopeTypeName);
                    int loaders = CountComponents<LevelSceneLoader>(scene);
                    int applicationUiManagers = CountComponents<ApplicationUIManager>(scene);
                    if (lifetimeScopes + loaders + applicationUiManagers != 0)
                    {
                        errors.Add(
                            $"Level {entry.LevelNumber} contains Bootstrap-owned application services "
                            + $"(scope={lifetimeScopes}, loader={loaders}, appUI={applicationUiManagers}).");
                    }

                    LevelSceneContext context = FindFirstComponent<LevelSceneContext>(scene);
                    if (context != null && context.LevelNumber != entry.LevelNumber)
                    {
                        errors.Add(
                            $"Level {entry.LevelNumber} catalog entry does not match "
                            + $"authored context {context.LevelNumber}.");
                    }

                    ValidateTowerNetworkObject(scene, entry.LevelNumber, errors);
                },
                errors);
        }

        private static void ValidateTowerNetworkObject(Scene scene, int levelNumber, List<string> errors)
        {
            TowerNetworkSceneAdapter adapter = FindFirstComponent<TowerNetworkSceneAdapter>(scene);
            if (adapter == null)
            {
                return;
            }

            GameObject owner = adapter.gameObject;
            if (owner.GetComponent<GridPlacementPresenter>() == null
                || owner.GetComponent<GameplayInputSource>() == null
                || owner.GetComponent<TowerInstanceFactory>() == null
                || owner.GetComponent<TowerLinkView>() == null
                || owner.GetComponent<TowerProjectilePoolView>() == null)
            {
                errors.Add(
                    $"Level {levelNumber} must keep placement, input, link, and projectile views "
                    + "together on the TowerNetworkSceneAdapter object.");
            }
        }

        private static void ValidateLevelLifetimeScope(Scene scene, int levelNumber, List<string> errors)
        {
            Component lifetimeScope = FindFirstComponentByFullName(scene, LevelLifetimeScopeTypeName);
            if (lifetimeScope == null)
            {
                return;
            }

            SerializedProperty autoRun = new SerializedObject(lifetimeScope).FindProperty("autoRun");
            if (autoRun.boolValue)
            {
                errors.Add(
                    $"Level {levelNumber} LevelLifetimeScope must disable Auto Run; "
                    + "LevelLoadSequence owns its parented Build call.");
            }
        }

        private static void InspectScene(string scenePath, Action<Scene> inspect, List<string> errors)
        {
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool openedByValidator = !scene.IsValid() || !scene.isLoaded;
            try
            {
                if (openedByValidator)
                {
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                }

                inspect(scene);
            }
            catch (Exception exception)
            {
                errors.Add($"Could not inspect scene '{scenePath}': {exception.Message}");
            }
            finally
            {
                if (openedByValidator && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void RequireExactlyOne<T>(Scene scene, string label, List<string> errors)
            where T : Component
        {
            int count = CountComponents<T>(scene);
            if (count != 1)
            {
                errors.Add($"Scene '{scene.path}' requires exactly one {label}; found {count}.");
            }
        }

        private static void RequireExactlyOneByFullName(
            Scene scene,
            string fullName,
            string label,
            List<string> errors)
        {
            int count = CountComponentsByFullName(scene, fullName);
            if (count != 1)
            {
                errors.Add($"Scene '{scene.path}' requires exactly one {label}; found {count}.");
            }
        }

        private static int CountComponents<T>(Scene scene)
            where T : Component
        {
            int count = 0;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                count += roots[index].GetComponentsInChildren<T>(true).Length;
            }

            return count;
        }

        private static T FindFirstComponent<T>(Scene scene)
            where T : Component
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                T component = roots[index].GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        private static int CountComponentsByFullName(Scene scene, string fullName)
        {
            int count = 0;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Component[] components = roots[rootIndex].GetComponentsInChildren<Component>(true);
                for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
                {
                    Component component = components[componentIndex];
                    if (component != null
                        && string.Equals(component.GetType().FullName, fullName, StringComparison.Ordinal))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static Component FindFirstComponentByFullName(Scene scene, string fullName)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Component[] components = roots[rootIndex].GetComponentsInChildren<Component>(true);
                for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
                {
                    Component component = components[componentIndex];
                    if (component != null
                        && string.Equals(component.GetType().FullName, fullName, StringComparison.Ordinal))
                    {
                        return component;
                    }
                }
            }

            return null;
        }
    }
}
