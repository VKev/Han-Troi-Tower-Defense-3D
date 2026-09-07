using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TowerDefense3D.Vfx.EditorTools
{
    /// <summary>
    /// Builds and refreshes the camera the effect warmup renders against, by copying the settings
    /// off a level's own camera.
    /// </summary>
    /// <remarks>
    /// The warmup only works if it draws against the same render setup gameplay draws against. A
    /// pipeline state is built for a particular colour format, depth format and sample count; warm
    /// one set and ask for another at runtime and the driver builds the real ones on the frame the
    /// player is watching - exactly the stall the warmup was added to remove, with the added cost
    /// of the warmup itself.
    ///
    /// So the settings are copied here rather than typed in, and
    /// <c>WarmupCameraMatchesLevelsTests</c> fails when a level drifts away from them. Authoring
    /// the warmup camera by hand would be a second copy of the level camera's settings, kept in
    /// step by memory.
    /// </remarks>
    public static class WarmupCameraSync
    {
        public const string PrefabPath = "Assets/Resources/Prefabs/WarmupCamera.prefab";

        private const string ReferenceScenePath = "Assets/Scenes/Levels/Level_001.unity";
        private const string LevelSceneFolder = "Assets/Scenes/Levels";

        /// <summary>
        /// Menu entry. Asks about unsaved work first, because syncing reopens scenes and would
        /// otherwise throw away edits the user had not saved.
        /// </summary>
        [MenuItem("Tools/Tower Defense/Sync Effect Warmup Camera")]
        public static void SyncFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Exit Play Mode before syncing the warmup camera.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Sync();
        }

        /// <summary>
        /// Does the work, assuming the caller has already dealt with unsaved scenes. Split from
        /// the menu entry so a script can run it without a dialog appearing out of nowhere.
        /// </summary>
        public static void Sync()
        {
            string openScene = EditorSceneManager.GetActiveScene().path;
            EditorSceneManager.OpenScene(ReferenceScenePath, OpenSceneMode.Single);

            Camera reference = FindLevelCamera(out Volume referenceVolume);
            if (reference == null)
            {
                Debug.LogError("No camera found in " + ReferenceScenePath);
                return;
            }

            var root = new GameObject("WarmupCamera");
            Camera camera = root.AddComponent<Camera>();

            // CopyFrom takes the whole camera, including the fields that decide the render target
            // format. What follows only overrides what must differ.
            camera.CopyFrom(reference);
            camera.enabled = true;

            // Behind everything, and drawing nothing of the scene: the warmup supplies its own
            // effects and must not depend on a level being loaded.
            camera.depth = -100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;

            CopyUniversalData(reference, camera);

            if (referenceVolume != null && referenceVolume.sharedProfile != null)
            {
                // Post-processing has pipeline states of its own, and they depend on which effects
                // the volume stack turns on. Warming without the level's profile would leave every
                // bloom pass cold.
                Volume volume = root.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = referenceVolume.priority;
                volume.weight = referenceVolume.weight;
                volume.sharedProfile = referenceVolume.sharedProfile;
            }

            // Read before the temporary object goes away; afterwards the Camera is destroyed and
            // touching it throws.
            string summary = "hdr=" + camera.allowHDR + " msaa=" + camera.allowMSAA;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();

            if (!string.IsNullOrEmpty(openScene))
            {
                EditorSceneManager.OpenScene(openScene, OpenSceneMode.Single);
            }

            Debug.Log(
                "Warmup camera synced from " + ReferenceScenePath + " to " + PrefabPath
                + "  (" + summary + ")");
        }

        /// <summary>Every level camera in the project, for the test to compare against.</summary>
        public static IReadOnlyList<string> CollectLevelScenePaths()
        {
            var paths = new List<string>();
            string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { LevelSceneFolder });
            for (int index = 0; index < guids.Length; index++)
            {
                paths.Add(AssetDatabase.GUIDToAssetPath(guids[index]));
            }

            paths.Sort();
            return paths;
        }

        public static Camera FindLevelCamera(out Volume globalVolume)
        {
            globalVolume = null;
            Camera found = null;
            foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (found == null)
                {
                    found = root.GetComponentInChildren<Camera>(true);
                }

                if (globalVolume == null)
                {
                    Volume[] volumes = root.GetComponentsInChildren<Volume>(true);
                    for (int index = 0; index < volumes.Length; index++)
                    {
                        if (volumes[index].isGlobal)
                        {
                            globalVolume = volumes[index];
                            break;
                        }
                    }
                }
            }

            return found;
        }

        private static void CopyUniversalData(Camera reference, Camera destination)
        {
            UniversalAdditionalCameraData source = reference.GetUniversalAdditionalCameraData();
            UniversalAdditionalCameraData target = destination.GetUniversalAdditionalCameraData();
            if (source == null || target == null)
            {
                return;
            }

            target.renderType = CameraRenderType.Base;
            target.renderPostProcessing = source.renderPostProcessing;
            target.antialiasing = source.antialiasing;
            target.antialiasingQuality = source.antialiasingQuality;
            target.requiresColorOption = source.requiresColorOption;
            target.requiresDepthOption = source.requiresDepthOption;
            // Which renderer the camera uses is serialized but has no public getter, so it is read
            // off the serialized object. It matters: two renderers mean two sets of render passes,
            // and warming against the wrong one warms the wrong pipeline states.
            var serialized = new SerializedObject(source);
            SerializedProperty rendererIndex = serialized.FindProperty("m_RendererIndex");
            if (rendererIndex != null)
            {
                target.SetRenderer(rendererIndex.intValue);
            }
            target.renderShadows = source.renderShadows;
            target.volumeLayerMask = source.volumeLayerMask;
        }
    }
}
