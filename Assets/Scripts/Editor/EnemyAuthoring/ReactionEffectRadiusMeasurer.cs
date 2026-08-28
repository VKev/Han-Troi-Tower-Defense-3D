using System.Collections.Generic;
using System.IO;
using TowerDefense3D.Enemies;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.GameFlow.Editor
{
    /// <summary>
    /// Measures the radius an element reaction effect actually covers on screen by rendering it
    /// with a top-down orthographic camera of known scale and reading the lit pixels back. The
    /// visible radius comes from the particle material and texture as much as from the shape
    /// module, so it cannot be read off the authored particle values.
    /// </summary>
    public static class ReactionEffectRadiusMeasurer
    {
        private const string EnemyPrefabFolder = "Assets/Resources/Prefabs/Enemies";
        private static string effectObjectName = "vfx_WindSwirl";
        private const string OutputFolder = "Temp/ReactionEffectRadius";
        private const int RenderSize = 512;
        private const float EnergyShare = 0.9f;
        private const float CameraHalfHeightMeters = 6f;
        private const int TimeSampleCount = 24;

        [MenuItem("Tools/Tower Defense/Measure Firestorm Effect Radius")]
        public static void MeasureFromMenu()
        {
            ReportWorldScales();
            MeasureVisibleRadius();
        }

        private static void ReportWorldScales()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { EnemyPrefabFolder });
            var report = new List<string>();
            for (int index = 0; index < guids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Transform effect = FindDeep(root.transform, effectObjectName);
                    report.Add(effect == null
                        ? Path.GetFileNameWithoutExtension(path) + "=missing"
                        : $"{Path.GetFileNameWithoutExtension(path)}={effect.lossyScale.x:0.###}");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            Debug.Log("Wind swirl world scale per enemy: " + string.Join(", ", report));
        }

        private static void MeasureVisibleRadius()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { EnemyPrefabFolder });
            if (guids.Length == 0)
            {
                Debug.LogError("No enemy prefabs found under " + EnemyPrefabFolder);
                return;
            }

            GameObject sample = PrefabUtility.LoadPrefabContents(
                AssetDatabase.GUIDToAssetPath(guids[0]));
            GameObject stage = null;
            Camera camera = null;
            RenderTexture renderTexture = null;
            try
            {
                Transform authored = FindDeep(sample.transform, effectObjectName);
                if (authored == null)
                {
                    Debug.LogError("No " + effectObjectName + " authored on the sampled enemy prefab.");
                    return;
                }

                float worldScale = authored.lossyScale.x;
                stage = new GameObject("ReactionEffectRadiusStage")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };

                var effect = Object.Instantiate(authored.gameObject, stage.transform);
                effect.transform.localPosition = Vector3.zero;
                effect.transform.localRotation = authored.localRotation;
                effect.transform.localScale = Vector3.one * worldScale;
                effect.SetActive(true);
                SetHideFlagsDeep(effect.transform);

                camera = CreateCamera(stage.transform);
                renderTexture = new RenderTexture(RenderSize, RenderSize, 24, RenderTextureFormat.ARGB32)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                camera.targetTexture = renderTexture;

                ParticleSystem[] systems = effect.GetComponentsInChildren<ParticleSystem>(true);
                float longestSeconds = 0f;
                for (int index = 0; index < systems.Length; index++)
                {
                    ParticleSystem.MainModule main = systems[index].main;
                    longestSeconds = Mathf.Max(
                        longestSeconds,
                        main.duration + main.startLifetime.constantMax);
                }

                if (longestSeconds <= 0f)
                {
                    longestSeconds = 1f;
                }

                float[] brightest = new float[RenderSize * RenderSize];
                var readback = new Texture2D(RenderSize, RenderSize, TextureFormat.RGBA32, false);
                for (int step = 0; step < TimeSampleCount; step++)
                {
                    float time = longestSeconds * (step + 1) / TimeSampleCount;
                    for (int index = 0; index < systems.Length; index++)
                    {
                        systems[index].Simulate(time, true, true);
                    }

                    camera.Render();
                    RenderTexture previous = RenderTexture.active;
                    RenderTexture.active = renderTexture;
                    readback.ReadPixels(new Rect(0, 0, RenderSize, RenderSize), 0, 0);
                    readback.Apply(false);
                    RenderTexture.active = previous;

                    Color32[] pixels = readback.GetPixels32();
                    for (int index = 0; index < pixels.Length; index++)
                    {
                        Color32 pixel = pixels[index];
                        float luminance = (pixel.r * 0.299f + pixel.g * 0.587f + pixel.b * 0.114f) / 255f;
                        if (luminance > brightest[index])
                        {
                            brightest[index] = luminance;
                        }
                    }
                }

                float metersPerPixel = CameraHalfHeightMeters * 2f / RenderSize;
                float peak = 0f;
                for (int index = 0; index < brightest.Length; index++)
                {
                    peak = Mathf.Max(peak, brightest[index]);
                }

                // Relative to the effect's own peak, because an additive particle fades out
                // gradually and an absolute cutoff would just measure the render exposure.
                float[] shares = { 0.10f, 0.25f, 0.50f };
                var radii = new float[shares.Length];
                for (int t = 0; t < shares.Length; t++)
                {
                    radii[t] = MeasureRadius(brightest, shares[t] * peak) * metersPerPixel;
                }

                float energyRadius = MeasureEnergyRadius(brightest) * metersPerPixel;

                Directory.CreateDirectory(OutputFolder);
                WritePng(brightest, radii, shares, energyRadius, metersPerPixel, peak);

                var summary = new List<string>();
                for (int t = 0; t < shares.Length; t++)
                {
                    summary.Add($"{shares[t]:P0} of peak -> {radii[t]:0.00}m");
                }

                summary.Add($"{EnergyShare:P0} of total light -> {energyRadius:0.00}m");

                Debug.Log(
                    $"{effectObjectName} visible radius at world scale {worldScale:0.###} "
                    + $"(peak luminance {peak:0.000}): "
                    + string.Join(", ", summary)
                    + $". Images written to {OutputFolder}.");

                Object.DestroyImmediate(readback);
            }
            finally
            {
                if (camera != null)
                {
                    camera.targetTexture = null;
                }

                if (renderTexture != null)
                {
                    renderTexture.Release();
                    Object.DestroyImmediate(renderTexture);
                }

                if (stage != null)
                {
                    Object.DestroyImmediate(stage);
                }

                PrefabUtility.UnloadPrefabContents(sample);
            }
        }

        private static Camera CreateCamera(Transform parent)
        {
            var owner = new GameObject("RadiusCamera") { hideFlags = HideFlags.HideAndDontSave };
            owner.transform.SetParent(parent, false);
            owner.transform.localPosition = new Vector3(0f, 12f, 0f);
            owner.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Camera camera = owner.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = CameraHalfHeightMeters;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = ~0;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;
            camera.enabled = false;
            return camera;
        }

        private static float MeasureRadius(float[] brightest, float threshold)
        {
            float center = (RenderSize - 1) * 0.5f;
            float furthest = 0f;
            for (int y = 0; y < RenderSize; y++)
            {
                for (int x = 0; x < RenderSize; x++)
                {
                    if (brightest[y * RenderSize + x] < threshold)
                    {
                        continue;
                    }

                    float dx = x - center;
                    float dy = y - center;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    if (distance > furthest)
                    {
                        furthest = distance;
                    }
                }
            }

            return furthest;
        }

        /// <summary>
        /// Radius of the disc that contains <see cref="EnergyShare"/> of all the light the effect
        /// emits. Insensitive to a faint outer haze in a way a brightness cutoff is not.
        /// </summary>
        private static float MeasureEnergyRadius(float[] brightest)
        {
            float center = (RenderSize - 1) * 0.5f;
            int maxRadius = Mathf.CeilToInt(RenderSize * 0.75f);
            var byRadius = new float[maxRadius + 1];
            float total = 0f;
            for (int y = 0; y < RenderSize; y++)
            {
                for (int x = 0; x < RenderSize; x++)
                {
                    float value = brightest[y * RenderSize + x];
                    if (value <= 0f)
                    {
                        continue;
                    }

                    float dx = x - center;
                    float dy = y - center;
                    int bucket = Mathf.Min(maxRadius, Mathf.RoundToInt(Mathf.Sqrt(dx * dx + dy * dy)));
                    byRadius[bucket] += value;
                    total += value;
                }
            }

            if (total <= 0f)
            {
                return 0f;
            }

            float running = 0f;
            for (int radius = 0; radius <= maxRadius; radius++)
            {
                running += byRadius[radius];
                if (running >= total * EnergyShare)
                {
                    return radius;
                }
            }

            return maxRadius;
        }

        private static void WritePng(
            float[] brightest,
            float[] radii,
            float[] thresholds,
            float energyRadius,
            float metersPerPixel,
            float peak)
        {
            var texture = new Texture2D(RenderSize, RenderSize, TextureFormat.RGBA32, false);
            var pixels = new Color32[brightest.Length];
            float exposure = peak > 0f ? 1f / peak : 1f;
            for (int index = 0; index < brightest.Length; index++)
            {
                // Normalised against the peak so the faint outer wisps are actually visible.
                byte value = (byte)Mathf.Clamp(
                    Mathf.RoundToInt(brightest[index] * exposure * 255f), 0, 255);
                pixels[index] = new Color32(value, value, value, 255);
            }

            // Authored gameplay radius in red, measured brightness contours in green.
            var reaction = AssetDatabase.LoadAssetAtPath<ElementReactionDefinition>(
                "Assets/Config/Combat/ElementReactions/FireWind_Firestorm.asset");
            if (reaction != null)
            {
                DrawCircle(pixels, reaction.RadiusMeters / metersPerPixel, new Color32(255, 40, 40, 255));
            }

            for (int t = 0; t < radii.Length; t++)
            {
                byte intensity = (byte)(120 + t * 45);
                DrawCircle(pixels, radii[t] / metersPerPixel, new Color32(40, intensity, 40, 255));
            }

            // The measure the radius is actually set from, in cyan.
            DrawCircle(pixels, energyRadius / metersPerPixel, new Color32(40, 220, 255, 255));

            texture.SetPixels32(pixels);
            texture.Apply(false);
            File.WriteAllBytes(Path.Combine(OutputFolder, "wind_swirl_radius.png"), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            var caption = new List<string>
            {
                $"metres per pixel: {metersPerPixel:0.0000}",
                $"peak luminance: {peak:0.000}",
                $"{EnergyShare:P0} of total light within: {energyRadius:0.00} m (cyan)"
            };
            for (int t = 0; t < radii.Length; t++)
            {
                caption.Add($"{thresholds[t]:P0} of peak: radius {radii[t]:0.00} m (green)");
            }

            if (reaction != null)
            {
                caption.Add($"authored radiusMeters: {reaction.RadiusMeters:0.00} m (red circle)");
            }

            File.WriteAllLines(Path.Combine(OutputFolder, "wind_swirl_radius.txt"), caption.ToArray());
        }

        private static void DrawCircle(Color32[] pixels, float radiusPixels, Color32 color)
        {
            if (radiusPixels <= 1f || radiusPixels >= RenderSize * 0.5f)
            {
                return;
            }

            float center = (RenderSize - 1) * 0.5f;
            int steps = Mathf.CeilToInt(radiusPixels * 8f);
            for (int step = 0; step < steps; step++)
            {
                float angle = step * Mathf.PI * 2f / steps;
                int x = Mathf.RoundToInt(center + Mathf.Cos(angle) * radiusPixels);
                int y = Mathf.RoundToInt(center + Mathf.Sin(angle) * radiusPixels);
                if (x < 0 || y < 0 || x >= RenderSize || y >= RenderSize)
                {
                    continue;
                }

                pixels[y * RenderSize + x] = color;
            }
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            for (int index = 0; index < root.childCount; index++)
            {
                Transform found = FindDeep(root.GetChild(index), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static void SetHideFlagsDeep(Transform root)
        {
            root.gameObject.hideFlags = HideFlags.HideAndDontSave;
            for (int index = 0; index < root.childCount; index++)
            {
                SetHideFlagsDeep(root.GetChild(index));
            }
        }
    }
}
