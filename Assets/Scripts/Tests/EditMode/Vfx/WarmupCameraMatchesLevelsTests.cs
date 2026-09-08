using System.Collections.Generic;
using NUnit.Framework;
using TowerDefense3D.Vfx.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TowerDefense3D.Tests.EditMode.Vfx
{
    /// <summary>
    /// Holds the effect warmup camera to the same render setup the levels use.
    /// </summary>
    /// <remarks>
    /// The warmup exists to build graphics pipeline states before the player sees an effect. A
    /// pipeline state is built against a specific colour format, depth format, sample count and
    /// post-processing chain, so warming against a camera configured differently from the level
    /// camera builds states gameplay never asks for. The stall comes back, the warmup still runs,
    /// and nothing anywhere reports a problem - the only symptom is the bug returning.
    ///
    /// That silence is why this is a test rather than a comment. Turning off post-processing on a
    /// level camera, or changing its anti-aliasing, now fails here instead of quietly costing the
    /// warmup its purpose.
    /// </remarks>
    public sealed class WarmupCameraMatchesLevelsTests
    {
        private string openScenePath;

        [SetUp]
        public void RememberOpenScene()
        {
            openScenePath = EditorSceneManager.GetActiveScene().path;
        }

        [TearDown]
        public void RestoreOpenScene()
        {
            if (!string.IsNullOrEmpty(openScenePath))
            {
                EditorSceneManager.OpenScene(openScenePath, OpenSceneMode.Single);
            }
        }

        [Test]
        public void WarmupCameraPrefabExists()
        {
            Assert.That(
                AssetDatabase.LoadAssetAtPath<GameObject>(WarmupCameraSync.PrefabPath),
                Is.Not.Null,
                "Missing " + WarmupCameraSync.PrefabPath
                + ". Run Tools/Tower Defense/Sync Effect Warmup Camera.");
        }

        [Test]
        public void EveryLevelCameraMatchesTheWarmupCamera()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WarmupCameraSync.PrefabPath);
            Assert.That(prefab, Is.Not.Null, "Missing " + WarmupCameraSync.PrefabPath);

            Camera warmup = prefab.GetComponentInChildren<Camera>(true);
            Assert.That(warmup, Is.Not.Null, "Warmup camera prefab has no Camera.");

            IReadOnlyList<string> scenePaths = WarmupCameraSync.CollectLevelScenePaths();
            Assert.That(scenePaths.Count, Is.GreaterThan(0), "No level scenes found.");

            var mismatches = new List<string>();
            for (int index = 0; index < scenePaths.Count; index++)
            {
                string scenePath = scenePaths[index];
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                Camera level = WarmupCameraSync.FindLevelCamera(out Volume volume);
                if (level == null)
                {
                    mismatches.Add(scenePath + ": no camera");
                    continue;
                }

                // Opening a scene invalidates Component wrappers loaded from a prefab asset.
                // Reload the prefab so Unity does not report its still-serialized URP data as null.
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WarmupCameraSync.PrefabPath);
                warmup = prefab.GetComponentInChildren<Camera>(true);
                UniversalAdditionalCameraData warmupData =
                    warmup.GetUniversalAdditionalCameraData();

                Compare(mismatches, scenePath, "allowHDR", warmup.allowHDR, level.allowHDR);
                Compare(mismatches, scenePath, "allowMSAA", warmup.allowMSAA, level.allowMSAA);

                UniversalAdditionalCameraData levelData = level.GetUniversalAdditionalCameraData();
                if (warmupData == null || levelData == null)
                {
                    mismatches.Add(scenePath + ": missing UniversalAdditionalCameraData");
                    continue;
                }

                Compare(
                    mismatches, scenePath, "renderPostProcessing",
                    warmupData.renderPostProcessing, levelData.renderPostProcessing);
                Compare(
                    mismatches, scenePath, "antialiasing",
                    warmupData.antialiasing, levelData.antialiasing);
                Compare(
                    mismatches, scenePath, "antialiasingQuality",
                    warmupData.antialiasingQuality, levelData.antialiasingQuality);
                Compare(
                    mismatches, scenePath, "requiresDepthOption",
                    warmupData.requiresDepthOption, levelData.requiresDepthOption);
                Compare(
                    mismatches, scenePath, "requiresColorOption",
                    warmupData.requiresColorOption, levelData.requiresColorOption);

                // The volume profile decides which post-processing passes run at all, and each
                // pass has pipeline states of its own. A level on a different profile warms a
                // different set of them.
                Volume warmupVolume = prefab.GetComponentInChildren<Volume>(true);
                string warmupProfile = warmupVolume == null || warmupVolume.sharedProfile == null
                    ? "none"
                    : warmupVolume.sharedProfile.name;
                string levelProfile = volume == null || volume.sharedProfile == null
                    ? "none"
                    : volume.sharedProfile.name;
                Compare(mismatches, scenePath, "global volume profile", warmupProfile, levelProfile);
            }

            Assert.That(
                mismatches,
                Is.Empty,
                "The warmup camera no longer matches these levels, so warming it builds pipeline "
                + "states gameplay will not use. Re-run Tools/Tower Defense/Sync Effect Warmup "
                + "Camera, or bring the level cameras back into line:\n  "
                + string.Join("\n  ", mismatches));
        }

        private static void Compare<T>(
            List<string> mismatches,
            string scenePath,
            string field,
            T warmupValue,
            T levelValue)
        {
            if (!EqualityComparer<T>.Default.Equals(warmupValue, levelValue))
            {
                mismatches.Add(
                    scenePath + ": " + field + " warmup=" + warmupValue + " level=" + levelValue);
            }
        }
    }
}
