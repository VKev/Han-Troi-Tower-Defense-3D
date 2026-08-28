using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.GameFlow.Editor
{
    /// <summary>
    /// Brings every runtime VFX prefab onto one mobile renderer budget. The dominant cost of
    /// these effects is overdraw - large alpha-blended quads with depth write off, stacked
    /// several layers deep - so the size clamp matters more than the particle count. Unity
    /// ships <c>maxParticleSize</c> at 0.5; several of these were authored at 32, which lets a
    /// single particle cover the screen many times over.
    /// </summary>
    public static class VfxMobileBudget
    {
        public const string VfxFolder = "Assets/Resources/Prefabs/VFX";

        /// <summary>
        /// Fraction of viewport height one particle may cover. Deliberately loose: a particle
        /// quad is usually larger than the glow drawn inside it, so clamping down to the size
        /// of the visible content cuts the art off at a hard circle. This ceiling only kills
        /// values no effect could justify - 32 lets one particle cover the screen 32 times -
        /// and leaves anything an artist plausibly authored alone.
        /// </summary>
        public const float MaxParticleSizeCeiling = 4f;

        /// <summary>Slack above the measured peak live count, for authoring headroom.</summary>
        private const float ReserveHeadroom = 1.5f;

        private const int MinimumReserve = 16;

        /// <summary>
        /// Cap for a system whose live count could not be measured. Not a measured value, just
        /// a bound: reserving thousands of particles for something that emits nothing observable
        /// is waste, but clamping it to a measured-looking number would be a guess.
        /// </summary>
        public const int UnmeasuredReserveCeiling = 256;

        /// <summary>How many points across the effect's life to sample the live count at.</summary>
        private const int SimulationSamples = 48;

        [MenuItem("Tools/Tower Defense/Apply VFX Mobile Budget")]
        public static void ApplyFromMenu()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { VfxFolder });
            var changes = new List<string>();
            var instanced = new HashSet<Material>();

            for (int index = 0; index < guids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (ApplyToPrefab(root, Path.GetFileNameWithoutExtension(path), changes, instanced))
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            if (changes.Count == 0)
            {
                Debug.Log("VFX mobile budget: already within budget, nothing changed.");
                return;
            }

            Debug.Log("VFX mobile budget applied:\n  " + string.Join("\n  ", changes));
        }

        private static bool ApplyToPrefab(
            GameObject root,
            string prefabName,
            List<string> changes,
            HashSet<Material> instanced)
        {
            bool dirty = false;

            ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
            for (int index = 0; index < systems.Length; index++)
            {
                ParticleSystem system = systems[index];
                int reserve = MeasurePeakLiveCount(system);
                ParticleSystem.MainModule main = system.main;
                if (main.maxParticles > reserve)
                {
                    changes.Add($"{prefabName}/{system.name}: maxParticles {main.maxParticles} -> {reserve}");
                    main.maxParticles = reserve;
                    dirty = true;
                }
            }

            var renderers = root.GetComponentsInChildren<ParticleSystemRenderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                ParticleSystemRenderer renderer = renderers[index];
                string label = prefabName + "/" + renderer.name;

                if (renderer.maxParticleSize > MaxParticleSizeCeiling)
                {
                    changes.Add($"{label}: maxParticleSize {renderer.maxParticleSize} -> {MaxParticleSizeCeiling}");
                    renderer.maxParticleSize = MaxParticleSizeCeiling;
                    dirty = true;
                }

                if (renderer.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off)
                {
                    changes.Add(label + ": cast shadows off");
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    dirty = true;
                }

                if (renderer.receiveShadows)
                {
                    changes.Add(label + ": receive shadows off");
                    renderer.receiveShadows = false;
                    dirty = true;
                }

                // Unlit particles never read either probe, so both lookups are pure cost.
                if (renderer.reflectionProbeUsage != UnityEngine.Rendering.ReflectionProbeUsage.Off)
                {
                    changes.Add(label + ": reflection probe off");
                    renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                    dirty = true;
                }

                if (renderer.lightProbeUsage != UnityEngine.Rendering.LightProbeUsage.Off)
                {
                    changes.Add(label + ": light probe off");
                    renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                    dirty = true;
                }

                // GPU instancing only ever applies to mesh render mode; billboard and stretch
                // modes are built into one dynamic mesh per system on the CPU.
                if (renderer.renderMode == ParticleSystemRenderMode.Mesh)
                {
                    EnableInstancing(renderer.sharedMaterial, label, changes, instanced);
                }
            }

            return dirty;
        }

        private static void EnableInstancing(
            Material material,
            string label,
            List<string> changes,
            HashSet<Material> instanced)
        {
            if (material == null || material.enableInstancing || !instanced.Add(material))
            {
                return;
            }

            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            changes.Add($"{label}: GPU instancing on {material.name}");
        }

        /// <summary>
        /// Peak number of particles the system actually has alive, found by simulating it across
        /// its whole life and reading the live count back. An emission formula cannot be trusted
        /// here: rate, bursts, sub-emitters and rate-over-distance interact, and clamping below
        /// the real peak silently culls particles and changes how the effect looks.
        /// </summary>
        private static int MeasurePeakLiveCount(ParticleSystem system)
        {
            ParticleSystem.MainModule main = system.main;
            int authoredCeiling = main.maxParticles;
            float span = main.duration + Mathf.Max(0f, main.startLifetime.constantMax);
            if (span <= 0f)
            {
                span = 1f;
            }

            int peak = 0;
            try
            {
                for (int step = 0; step <= SimulationSamples; step++)
                {
                    float time = span * step / SimulationSamples;
                    system.Simulate(time, true, true);
                    peak = Mathf.Max(peak, system.particleCount);
                }
            }
            finally
            {
                system.Clear(true);
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            if (peak <= 0)
            {
                // Nothing measurable across its whole life: bound it instead of guessing a
                // count, so the buffer stops being thousands without pretending we measured.
                return Mathf.Min(authoredCeiling, UnmeasuredReserveCeiling);
            }

            int reserve = Mathf.CeilToInt(peak * ReserveHeadroom);
            return Mathf.Clamp(Mathf.Max(MinimumReserve, reserve), 1, authoredCeiling);
        }
    }
}
