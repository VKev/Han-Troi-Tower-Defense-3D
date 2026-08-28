using System.Collections.Generic;
using NUnit.Framework;
using TowerDefense3D.GameFlow.Editor;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.Enemies.Tests.EditMode
{
    /// <summary>
    /// Guards the mobile renderer budget applied by
    /// <see cref="VfxMobileBudget"/>. These are asset settings, so nothing stops a later
    /// authoring pass from putting them back; this fails when it does.
    /// </summary>
    public sealed class VfxRendererBudgetTests
    {
        /// <summary>
        /// Reserving far more particles than a system emits only wastes buffers. Shared with the
        /// tool so the guard and the fix cannot drift apart.
        /// </summary>
        private const int MaxParticlesCeiling = VfxMobileBudget.UnmeasuredReserveCeiling;

        [Test]
        public void EveryVfxRenderer_StaysWithinTheMobileBudget()
        {
            var offenders = new List<string>();

            ForEachVfxPrefab((prefabName, root) =>
            {
                var renderers = root.GetComponentsInChildren<ParticleSystemRenderer>(true);
                for (int index = 0; index < renderers.Length; index++)
                {
                    ParticleSystemRenderer renderer = renderers[index];
                    string label = prefabName + "/" + renderer.name;

                    if (renderer.maxParticleSize > VfxMobileBudget.MaxParticleSizeCeiling)
                    {
                        offenders.Add(
                            $"{label}: maxParticleSize {renderer.maxParticleSize} exceeds "
                            + $"{VfxMobileBudget.MaxParticleSizeCeiling}");
                    }

                    if (renderer.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off)
                    {
                        offenders.Add(label + ": casts shadows");
                    }

                    if (renderer.receiveShadows)
                    {
                        offenders.Add(label + ": receives shadows");
                    }

                    if (renderer.reflectionProbeUsage != UnityEngine.Rendering.ReflectionProbeUsage.Off)
                    {
                        offenders.Add(label + ": uses a reflection probe");
                    }

                    if (renderer.lightProbeUsage != UnityEngine.Rendering.LightProbeUsage.Off)
                    {
                        offenders.Add(label + ": uses a light probe");
                    }
                }
            });

            Assert.That(
                offenders,
                Is.Empty,
                "Run Tools > Tower Defense > Apply VFX Mobile Budget.\n"
                + string.Join("\n", offenders));
        }

        [Test]
        public void EveryVfxParticleSystem_ReservesAPlausibleParticleCount()
        {
            var offenders = new List<string>();

            ForEachVfxPrefab((prefabName, root) =>
            {
                ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
                for (int index = 0; index < systems.Length; index++)
                {
                    ParticleSystem system = systems[index];
                    int reserved = system.main.maxParticles;
                    if (reserved > MaxParticlesCeiling)
                    {
                        offenders.Add($"{prefabName}/{system.name}: maxParticles {reserved} exceeds {MaxParticlesCeiling}");
                    }
                }
            });

            Assert.That(
                offenders,
                Is.Empty,
                "Run Tools > Tower Defense > Apply VFX Mobile Budget.\n"
                + string.Join("\n", offenders));
        }

        [Test]
        public void EveryMeshModeVfxMaterial_HasGpuInstancingEnabled()
        {
            var offenders = new List<string>();

            ForEachVfxPrefab((prefabName, root) =>
            {
                var renderers = root.GetComponentsInChildren<ParticleSystemRenderer>(true);
                for (int index = 0; index < renderers.Length; index++)
                {
                    ParticleSystemRenderer renderer = renderers[index];

                    // Instancing only exists for mesh mode; billboard and stretch modes are
                    // built into one dynamic mesh per system on the CPU.
                    if (renderer.renderMode != ParticleSystemRenderMode.Mesh)
                    {
                        continue;
                    }

                    Material material = renderer.sharedMaterial;
                    if (material != null && !material.enableInstancing)
                    {
                        offenders.Add($"{prefabName}/{renderer.name}: {material.name} has instancing off");
                    }
                }
            });

            Assert.That(
                offenders,
                Is.Empty,
                "Run Tools > Tower Defense > Apply VFX Mobile Budget.\n"
                + string.Join("\n", offenders));
        }

        private static void ForEachVfxPrefab(System.Action<string, GameObject> inspect)
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:Prefab",
                new[] { VfxMobileBudget.VfxFolder });
            Assert.That(guids, Is.Not.Empty, "No VFX prefabs found under " + VfxMobileBudget.VfxFolder);

            for (int index = 0; index < guids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    inspect(System.IO.Path.GetFileNameWithoutExtension(path), root);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }
    }
}
