using System.Collections.Generic;
using System.IO;
using TowerDefense3D.Enemies;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.GameFlow.Editor
{
    /// <summary>
    /// Points every authored enemy prefab at the reaction effect prefabs it should play, and
    /// removes the per-enemy nested copies an earlier wiring created. Reactions now play from one
    /// rig shared by all enemies, so the reference is to the prefab asset rather than to a copy
    /// living inside the enemy. Also attaches the thermal shield view where a shield mesh exists.
    /// </summary>
    public static class EnemyReactionEffectWiring
    {
        private const string EnemyPrefabFolder = "Assets/Resources/Prefabs/Enemies";

        // Nested effect copies from earlier wiring. Reaction effects now play from one shared
        // rig at runtime, so a per-enemy copy is dead weight: it never renders and it multiplies
        // the particle renderer count in every enemy prefab.
        private static readonly string[] RetiredEffectObjectNames =
        {
            "VFX_WaterSplash",
            "vfx_WindSwirl",
            "VFX_WaterKnock",
            "VFX_SocNhiet"
        };

        private sealed class ReactionEffectPlan
        {
            public ReactionEffectPlan(
                ElementReactionId reactionId,
                string prefabPath,
                EnemyElementEffectPlacement placement,
                float durationSeconds)
            {
                ReactionId = reactionId;
                PrefabPath = prefabPath;
                Placement = placement;
                DurationSeconds = durationSeconds;
            }

            public ElementReactionId ReactionId { get; }
            public string PrefabPath { get; }
            public EnemyElementEffectPlacement Placement { get; }
            public float DurationSeconds { get; }
        }

        private static readonly ReactionEffectPlan[] Plans =
        {
            new ReactionEffectPlan(
                ElementReactionId.ThermalShock,
                "Assets/Resources/Prefabs/VFX/VFX_SocNhiet.prefab",
                EnemyElementEffectPlacement.EnemyPosition,
                0.5f),
            new ReactionEffectPlan(
                ElementReactionId.Firestorm,
                "Assets/Resources/Prefabs/VFX/vfx_WindSwirl.prefab",
                EnemyElementEffectPlacement.EnemyPosition,
                2f),
            new ReactionEffectPlan(
                ElementReactionId.WaterLift,
                "Assets/Resources/Prefabs/VFX/VFX_WaterKnock.prefab",
                EnemyElementEffectPlacement.EnemyFeet,
                1f)
        };

        [MenuItem("Tools/Tower Defense/Wire Enemy Reaction Effects")]
        public static void WireFromMenu()
        {
            var prefabs = new List<GameObject>();
            for (int index = 0; index < Plans.Length; index++)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Plans[index].PrefabPath);
                if (asset == null)
                {
                    Debug.LogError("Reaction effect prefab is missing at " + Plans[index].PrefabPath);
                    return;
                }

                prefabs.Add(asset);
            }

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { EnemyPrefabFolder });
            var wired = new List<string>();
            for (int index = 0; index < guids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (TryWire(root, prefabs))
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        wired.Add(Path.GetFileNameWithoutExtension(path));
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Reaction effects wired on {wired.Count} enemy prefab(s): {string.Join(", ", wired)}.");
        }

        private static bool TryWire(GameObject root, List<GameObject> prefabs)
        {
            var view = root.GetComponentInChildren<EnemyElementEffectView>(true);
            if (view == null)
            {
                return false;
            }

            var serialized = new SerializedObject(view);
            SerializedProperty entries = serialized.FindProperty("reactionEffects");
            if (entries == null || !entries.isArray)
            {
                Debug.LogError(root.name + " has no serialized reactionEffects array.");
                return false;
            }

            RemoveRetiredEffects(root.transform);
            for (int index = 0; index < Plans.Length; index++)
            {
                ReactionEffectPlan plan = Plans[index];
                SerializedProperty entry = FindOrAppendEntry(entries, (int)plan.ReactionId);
                entry.FindPropertyRelative("reactionId").enumValueIndex = (int)plan.ReactionId;
                entry.FindPropertyRelative("effect").objectReferenceValue = prefabs[index];
                entry.FindPropertyRelative("placement").enumValueIndex = (int)plan.Placement;
                entry.FindPropertyRelative("priority").intValue = 0;
                entry.FindPropertyRelative("durationSeconds").floatValue = plan.DurationSeconds;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            WireThermalShield(root);
            return true;
        }


        /// <summary>
        /// Enemies that carry a shield mesh get the view that fades it with the remaining
        /// thermal shield hits and flashes it while a thermal shock is breaking it.
        /// </summary>
        private static void WireThermalShield(GameObject root)
        {
            Transform shield = FindShieldRoot(root.transform);
            var view = root.GetComponentInChildren<EnemyThermalShieldView>(true);
            if (shield == null)
            {
                if (view != null)
                {
                    Object.DestroyImmediate(view, true);
                }

                return;
            }

            if (view == null)
            {
                view = root.AddComponent<EnemyThermalShieldView>();
            }

            var serialized = new SerializedObject(view);
            serialized.FindProperty("shieldRoot").objectReferenceValue = shield.gameObject;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform FindShieldRoot(Transform root)
        {
            if (root.name.IndexOf("shield", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return root;
            }

            for (int index = 0; index < root.childCount; index++)
            {
                Transform found = FindShieldRoot(root.GetChild(index));
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static void RemoveRetiredEffects(Transform root)
        {
            for (int index = 0; index < RetiredEffectObjectNames.Length; index++)
            {
                Transform retired = FindByName(root, RetiredEffectObjectNames[index]);
                while (retired != null)
                {
                    Object.DestroyImmediate(retired.gameObject, true);
                    retired = FindByName(root, RetiredEffectObjectNames[index]);
                }
            }
        }

        private static Transform FindByName(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            for (int index = 0; index < root.childCount; index++)
            {
                Transform found = FindByName(root.GetChild(index), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static SerializedProperty FindOrAppendEntry(SerializedProperty entries, int reactionId)
        {
            for (int index = 0; index < entries.arraySize; index++)
            {
                SerializedProperty candidate = entries.GetArrayElementAtIndex(index);
                if (candidate.FindPropertyRelative("reactionId").enumValueIndex == reactionId)
                {
                    return candidate;
                }
            }

            entries.arraySize++;
            return entries.GetArrayElementAtIndex(entries.arraySize - 1);
        }
    }
}
