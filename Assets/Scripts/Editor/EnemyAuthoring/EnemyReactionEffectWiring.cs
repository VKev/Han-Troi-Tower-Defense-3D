using System.Collections.Generic;
using System.IO;
using TowerDefense3D.Enemies;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.GameFlow.Editor
{
    /// <summary>
    /// Wires element reaction effects onto every authored enemy prefab. Reaction effects are
    /// nested prefab instances that the view toggles rather than asset references it
    /// instantiates, so each enemy needs its own copy. Enemy rigs are scaled differently, so
    /// each copy is also given the local scale that lands on one shared world size - otherwise
    /// the same effect covers twice the ground on a boss as it does on a basic enemy, and no
    /// single authored reaction radius can match what the player sees.
    /// </summary>
    public static class EnemyReactionEffectWiring
    {
        private const string EnemyPrefabFolder = "Assets/Resources/Prefabs/Enemies";

        // Effect objects an earlier wiring left behind; removed so nothing dead ships.
        private static readonly string[] RetiredEffectObjectNames = { "VFX_WaterSplash" };

        private sealed class ReactionEffectPlan
        {
            public ReactionEffectPlan(
                ElementReactionId reactionId,
                string prefabPath,
                string objectName,
                EnemyElementEffectPlacement placement,
                float durationSeconds,
                float worldScale)
            {
                ReactionId = reactionId;
                PrefabPath = prefabPath;
                ObjectName = objectName;
                Placement = placement;
                DurationSeconds = durationSeconds;
                WorldScale = worldScale;
            }

            public ElementReactionId ReactionId { get; }
            public string PrefabPath { get; }
            public string ObjectName { get; }
            public EnemyElementEffectPlacement Placement { get; }
            public float DurationSeconds { get; }
            public float WorldScale { get; }
        }

        private static readonly ReactionEffectPlan[] Plans =
        {
            new ReactionEffectPlan(
                ElementReactionId.Firestorm,
                "Assets/Resources/Prefabs/VFX/vfx_WindSwirl.prefab",
                "vfx_WindSwirl",
                EnemyElementEffectPlacement.EnemyPosition,
                2f,
                1.54f),
            new ReactionEffectPlan(
                ElementReactionId.WaterLift,
                "Assets/Resources/Prefabs/VFX/VFX_WaterKnock.prefab",
                "VFX_WaterKnock",
                EnemyElementEffectPlacement.EnemyFeet,
                1f,
                1.54f)
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

            Transform parent = FindReactionEffectParent(view, entries);
            RemoveRetiredEffects(root.transform);
            for (int index = 0; index < Plans.Length; index++)
            {
                ReactionEffectPlan plan = Plans[index];
                GameObject effect = EnsureInstance(parent, prefabs[index], plan);
                SerializedProperty entry = FindOrAppendEntry(entries, (int)plan.ReactionId);
                entry.FindPropertyRelative("reactionId").enumValueIndex = (int)plan.ReactionId;
                entry.FindPropertyRelative("effect").objectReferenceValue = effect;
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
                if (retired != null)
                {
                    Object.DestroyImmediate(retired.gameObject, true);
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

        private static Transform FindReactionEffectParent(
            EnemyElementEffectView view,
            SerializedProperty entries)
        {
            for (int index = 0; index < entries.arraySize; index++)
            {
                var existing = entries.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("effect")
                    .objectReferenceValue as GameObject;
                if (existing != null && existing.transform.parent != null)
                {
                    return existing.transform.parent;
                }
            }

            return view.transform;
        }

        private static GameObject EnsureInstance(
            Transform parent,
            GameObject prefab,
            ReactionEffectPlan plan)
        {
            Transform existing = parent.Find(plan.ObjectName);
            GameObject instance;
            if (existing != null)
            {
                instance = existing.gameObject;
            }
            else
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                instance.name = plan.ObjectName;
            }

            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            ApplyWorldScale(instance.transform, plan.WorldScale);
            instance.SetActive(false);
            return instance;
        }

        /// <summary>
        /// Cancels the parent rig's scale so the effect lands on the requested world size.
        /// </summary>
        private static void ApplyWorldScale(Transform target, float worldScale)
        {
            target.localScale = Vector3.one;
            Vector3 parentScale = target.parent != null ? target.parent.lossyScale : Vector3.one;
            target.localScale = new Vector3(
                worldScale / Mathf.Max(0.0001f, parentScale.x),
                worldScale / Mathf.Max(0.0001f, parentScale.y),
                worldScale / Mathf.Max(0.0001f, parentScale.z));
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
