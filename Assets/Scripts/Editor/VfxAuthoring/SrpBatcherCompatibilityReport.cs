using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.GameFlow.Editor
{
    /// <summary>
    /// Asks Unity itself whether each shader the project renders with is SRP Batcher compatible,
    /// and reports what actually consumes the draw calls. Guessing from shader source is not
    /// reliable: compatibility depends on how properties land in reflected constant buffers, and
    /// a renderer can also be pushed out of the batcher at runtime by a MaterialPropertyBlock.
    /// </summary>
    public static class SrpBatcherCompatibilityReport
    {
        [MenuItem("Tools/Tower Defense/Report SRP Batcher Compatibility")]
        public static void ReportFromMenu()
        {
            MethodInfo compatibility = typeof(ShaderUtil).GetMethod(
                "GetSRPBatcherCompatibilityCode",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

            var report = new StringBuilder();
            report.AppendLine("SRP Batcher compatibility (0 = compatible):");

            var seen = new HashSet<Shader>();
            foreach (Material material in CollectRenderedMaterials())
            {
                if (material == null || material.shader == null || !seen.Add(material.shader))
                {
                    continue;
                }

                string verdict = "unknown (API unavailable)";
                if (compatibility != null)
                {
                    object code = compatibility.Invoke(null, new object[] { material.shader, 0 });
                    verdict = (int)code == 0
                        ? "COMPATIBLE"
                        : "NOT compatible (code " + code + ")";
                }

                report.AppendLine($"  {material.shader.name} -> {verdict}");
            }

            report.AppendLine();
            report.AppendLine(DescribeRendererMix());
            Debug.Log(report.ToString());
        }

        private static IEnumerable<Material> CollectRenderedMaterials()
        {
            var materials = new List<Material>();
            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
            for (int index = 0; index < guids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material != null)
                {
                    materials.Add(material);
                }
            }

            return materials;
        }

        /// <summary>
        /// Counts what the prefabs actually ship, because only mesh and skinned-mesh renderers can
        /// ever enter the SRP Batcher; particle renderers are dynamic geometry and never do.
        /// </summary>
        private static string DescribeRendererMix()
        {
            string[] folders = { "Assets/Resources/Prefabs", "Assets/Plugins/VFXCuaAnhHai" };
            int mesh = 0;
            int skinned = 0;
            int particle = 0;
            var particleHeavy = new List<string>();

            string[] guids = AssetDatabase.FindAssets("t:Prefab", folders);
            for (int index = 0; index < guids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null)
                {
                    continue;
                }

                int localParticles = root.GetComponentsInChildren<ParticleSystemRenderer>(true).Length;
                mesh += root.GetComponentsInChildren<MeshRenderer>(true).Length;
                skinned += root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
                particle += localParticles;

                if (localParticles >= 3)
                {
                    particleHeavy.Add($"{System.IO.Path.GetFileNameWithoutExtension(path)} ({localParticles})");
                }
            }

            var summary = new StringBuilder();
            summary.AppendLine("Renderer mix across prefabs:");
            summary.AppendLine($"  MeshRenderer        {mesh}  (can SRP batch / instance)");
            summary.AppendLine($"  SkinnedMeshRenderer {skinned}  (can SRP batch)");
            summary.AppendLine($"  ParticleSystem      {particle}  (never SRP batches)");
            summary.AppendLine("Prefabs with three or more particle renderers, one draw each:");
            summary.AppendLine("  " + string.Join(", ", particleHeavy));
            return summary.ToString();
        }
    }
}
