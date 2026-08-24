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

                    RequireExactlyOne<ApplicationUIView>(scene, "ApplicationUIView", errors);
                    RequireExactlyOne<SafeAreaView>(scene, "SafeAreaView", errors);
                    RequireExactlyOne<EventSystem>(scene, "EventSystem", errors);

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
                    RequireExactlyOneByFullName(scene, LevelLifetimeScopeTypeName, "LevelLifetimeScope", errors);
                    ValidateLevelLifetimeScope(scene, entry.LevelNumber, errors);
                    RequireExactlyOne<BoardView>(scene, "BoardView", errors);
                    RequireExactlyOne<BoardCameraView>(scene, "BoardCameraView", errors);
                    RequireExactlyOne<GameplayInputSource>(scene, "GameplayInputSource", errors);
                    RequireExactlyOne<GridPlacementView>(scene, "GridPlacementView", errors);
                    RequireExactlyOne<GameplayUIView>(scene, "GameplayUIView", errors);
                    RequireExactlyOne<PlacementHudView>(scene, "PlacementHudView", errors);
                    RequireExactlyOne<TowerNetworkHudView>(scene, "TowerNetworkHudView", errors);
                    RequireExactlyOne<TowerInstanceFactory>(scene, "TowerInstanceFactory", errors);
                    RequireExactlyOne<GridPlacementPresenter>(scene, "GridPlacementPresenter", errors);
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
                    int applicationUiViews = CountComponents<ApplicationUIView>(scene);
                    if (lifetimeScopes + applicationUiViews != 0)
                    {
                        errors.Add(
                            $"Level {entry.LevelNumber} contains Bootstrap-owned application services "
                            + $"(scope={lifetimeScopes}, appUI={applicationUiViews}).");
                    }
                },
                errors);
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
                    + "VContainerLevelSceneGateway owns its parented Build call.");
            }

            SerializedProperty authoredLevelNumber =
                new SerializedObject(lifetimeScope).FindProperty("levelNumber");
            if (authoredLevelNumber == null || authoredLevelNumber.intValue != levelNumber)
            {
                errors.Add(
                    $"Level {levelNumber} catalog entry does not match its authored LevelLifetimeScope number.");
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
