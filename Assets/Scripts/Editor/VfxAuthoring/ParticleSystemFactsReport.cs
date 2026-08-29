using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.GameFlow.Editor
{
    /// <summary>
    /// Dumps the facts that decide whether a particle system can move to a shared global emitter:
    /// simulation space, looping, emission shape, and whether another system drives it as a
    /// sub-emitter. Read through the ParticleSystem API rather than the serialised file, because
    /// the YAML field names do not map cleanly onto the enums.
    /// </summary>
    public static class ParticleSystemFactsReport
    {
        private static readonly string[] Folders =
        {
            "Assets/Resources/Prefabs/VFX"
        };

        [MenuItem("Tools/Tower Defense/Report Particle System Facts")]
        public static void ReportFromMenu()
        {
            var report = new StringBuilder();
            report.AppendLine("Particle system facts (space / loop / emission / driven-by-sub):");

            string[] guids = AssetDatabase.FindAssets("t:Prefab", Folders);
            for (int index = 0; index < guids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null)
                {
                    continue;
                }

                ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
                if (systems.Length == 0)
                {
                    continue;
                }

                HashSet<ParticleSystem> driven = CollectSubEmitters(systems);
                report.AppendLine("=== " + Path.GetFileNameWithoutExtension(path));
                for (int s = 0; s < systems.Length; s++)
                {
                    ParticleSystem system = systems[s];
                    ParticleSystem.MainModule main = system.main;
                    ParticleSystem.EmissionModule emission = system.emission;

                    string bursts = emission.burstCount == 0 ? "-" : DescribeBursts(emission);
                    report.AppendLine(string.Format(
                        "  {0,-20} space={1,-6} loop={2,-5} rate={3,-6} bursts={4,-10} shape={5,-5} drivenBySub={6}",
                        system.name,
                        main.simulationSpace,
                        main.loop,
                        emission.rateOverTime.constantMax,
                        bursts,
                        system.shape.enabled,
                        driven.Contains(system)));
                }
            }

            Debug.Log(report.ToString());
        }

        private static string DescribeBursts(ParticleSystem.EmissionModule emission)
        {
            var bursts = new ParticleSystem.Burst[emission.burstCount];
            emission.GetBursts(bursts);
            var parts = new List<string>(bursts.Length);
            for (int index = 0; index < bursts.Length; index++)
            {
                ParticleSystem.Burst burst = bursts[index];
                parts.Add($"t{burst.time:0.##}x{burst.maxCount}");
            }

            return string.Join(",", parts);
        }

        /// <summary>
        /// Systems referenced by another system's sub-emitter module. A global emitter must never
        /// emit into these directly - their parent particles trigger them.
        /// </summary>
        private static HashSet<ParticleSystem> CollectSubEmitters(ParticleSystem[] systems)
        {
            var driven = new HashSet<ParticleSystem>();
            for (int index = 0; index < systems.Length; index++)
            {
                ParticleSystem.SubEmittersModule sub = systems[index].subEmitters;
                if (!sub.enabled)
                {
                    continue;
                }

                for (int slot = 0; slot < sub.subEmittersCount; slot++)
                {
                    ParticleSystem child = sub.GetSubEmitterSystem(slot);
                    if (child != null)
                    {
                        driven.Add(child);
                    }
                }
            }

            return driven;
        }
    }
}
