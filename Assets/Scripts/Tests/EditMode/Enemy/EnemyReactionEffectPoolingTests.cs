using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.Enemies.Tests.EditMode
{
    /// <summary>
    /// Reaction effects play from one rig shared by every enemy, so the authored reference must be
    /// to a prefab asset. A reference to an object nested inside the enemy would mean the old
    /// per-enemy copy is back, multiplying particle renderers by the number of enemy types.
    /// Element marks are the opposite: their particles follow the enemy, so they stay nested.
    /// </summary>
    public sealed class EnemyReactionEffectPoolingTests
    {
        private const string EnemyPrefabFolder = "Assets/Resources/Prefabs/Enemies";

        [Test]
        public void EveryReactionEffect_ReferencesAPrefabAssetRatherThanANestedCopy()
        {
            var offenders = new List<string>();

            ForEachEnemyPrefab((prefabName, root, view) =>
            {
                var serialized = new SerializedObject(view);
                SerializedProperty entries = serialized.FindProperty("reactionEffects");
                Assert.That(entries.arraySize, Is.GreaterThan(0), prefabName + " has no reaction effects.");

                for (int index = 0; index < entries.arraySize; index++)
                {
                    var effect = entries.GetArrayElementAtIndex(index)
                        .FindPropertyRelative("effect")
                        .objectReferenceValue as GameObject;

                    if (effect == null)
                    {
                        offenders.Add($"{prefabName}: reaction effect {index} is unassigned");
                        continue;
                    }

                    if (effect.transform.IsChildOf(root.transform))
                    {
                        offenders.Add($"{prefabName}/{effect.name}: nested copy instead of a prefab asset");
                    }
                }
            });

            Assert.That(
                offenders,
                Is.Empty,
                "Run Tools > Tower Defense > Wire Enemy Reaction Effects.\n"
                + string.Join("\n", offenders));
        }

        [Test]
        public void ElementMarks_StayNestedBecauseTheirParticlesFollowTheEnemy()
        {
            var offenders = new List<string>();
            string[] markFields = { "fireEffect", "waterEffect", "windEffect" };

            ForEachEnemyPrefab((prefabName, root, view) =>
            {
                var serialized = new SerializedObject(view);
                for (int index = 0; index < markFields.Length; index++)
                {
                    var mark = serialized.FindProperty(markFields[index]).objectReferenceValue as GameObject;
                    if (mark == null)
                    {
                        continue;
                    }

                    if (!mark.transform.IsChildOf(root.transform))
                    {
                        offenders.Add($"{prefabName}/{markFields[index]}: must stay nested in the enemy");
                    }
                }
            });

            Assert.That(offenders, Is.Empty, string.Join("\n", offenders));
        }

        private static void ForEachEnemyPrefab(
            System.Action<string, GameObject, EnemyElementEffectView> inspect)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { EnemyPrefabFolder });
            Assert.That(guids, Is.Not.Empty, "No enemy prefabs found under " + EnemyPrefabFolder);

            for (int index = 0; index < guids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var view = root.GetComponentInChildren<EnemyElementEffectView>(true);
                    if (view != null)
                    {
                        inspect(System.IO.Path.GetFileNameWithoutExtension(path), root, view);
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }
    }
}
