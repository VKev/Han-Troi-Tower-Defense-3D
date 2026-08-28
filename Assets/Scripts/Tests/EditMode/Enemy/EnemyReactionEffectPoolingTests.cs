using NUnit.Framework;
using TowerDefense3D.Towers;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.Enemies.Tests.EditMode
{
    /// <summary>
    /// Reaction effects are nested inside each pooled enemy rather than instantiated per event,
    /// so they are pooled with the enemy. Detached placements temporarily unparent the effect,
    /// which is the one thing that can leak it out of the pooled hierarchy.
    /// </summary>
    public sealed class EnemyReactionEffectPoolingTests
    {
        private const string BasicEnemyPrefabPath = "Assets/Resources/Prefabs/Enemies/BasicEnemy.prefab";

        [Test]
        public void DetachedReactionEffect_ReturnsUnderItsAuthoredParentWhenTheEnemyIsReleased()
        {
            GameObject owner = PrefabUtility.LoadPrefabContents(BasicEnemyPrefabPath);
            try
            {
                var view = owner.GetComponentInChildren<EnemyElementEffectView>(true);
                Assert.That(view, Is.Not.Null);

                Transform effect = FindDeep(owner.transform, "VFX_WaterKnock");
                Assert.That(effect, Is.Not.Null, "The lift effect must be nested in the enemy prefab.");

                Transform authoredParent = effect.parent;
                Vector3 authoredLocalPosition = effect.localPosition;
                Assert.That(authoredParent, Is.Not.Null);
                Assert.That(effect.gameObject.activeSelf, Is.False, "Effects must start switched off.");

                view.Bind(default);
                view.ShowReaction(new ElementReactionEvent(
                    1L,
                    ElementReactionId.WaterLift,
                    new ElementPair(ElementType.Water, ElementType.Wind),
                    new Vector3(5f, 0f, 7f),
                    1f));

                Assert.That(effect.gameObject.activeSelf, Is.True, "The reaction must switch the effect on.");
                Assert.That(
                    effect.parent,
                    Is.Null,
                    "A feet-placed effect detaches so it stays on the ground while the enemy is lifted.");

                view.Release();

                Assert.That(
                    effect.parent,
                    Is.EqualTo(authoredParent),
                    "Releasing the enemy must pull the effect back into the pooled hierarchy.");
                Assert.That(effect.localPosition, Is.EqualTo(authoredLocalPosition));
                Assert.That(effect.gameObject.activeSelf, Is.False);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(owner);
            }
        }

        [Test]
        public void EveryReactionEffect_IsNestedInTheEnemySoNothingIsInstantiatedPerEvent()
        {
            GameObject owner = PrefabUtility.LoadPrefabContents(BasicEnemyPrefabPath);
            try
            {
                var view = owner.GetComponentInChildren<EnemyElementEffectView>(true);
                var serialized = new SerializedObject(view);
                SerializedProperty entries = serialized.FindProperty("reactionEffects");

                Assert.That(entries.arraySize, Is.GreaterThan(0));
                for (int index = 0; index < entries.arraySize; index++)
                {
                    var effect = entries.GetArrayElementAtIndex(index)
                        .FindPropertyRelative("effect")
                        .objectReferenceValue as GameObject;

                    Assert.That(effect, Is.Not.Null, $"Reaction effect {index} is unassigned.");
                    Assert.That(
                        effect.transform.IsChildOf(owner.transform),
                        Is.True,
                        $"{effect.name} must live inside the enemy prefab to be pooled with it.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(owner);
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
    }
}
