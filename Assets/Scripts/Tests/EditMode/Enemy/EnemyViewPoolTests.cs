using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.Enemies.Tests.EditMode
{
    public sealed class EnemyViewPoolTests
    {
        private GameObject poolObject;
        private GameObject firstPrefab;
        private GameObject secondPrefab;
        private EnemyDefinition firstDefinition;
        private EnemyDefinition secondDefinition;

        [SetUp]
        public void SetUp()
        {
            poolObject = new GameObject("Enemy View Pool");
            firstPrefab = CreateViewPrefab("First Enemy View");
            secondPrefab = CreateViewPrefab("Second Enemy View");
            firstDefinition = CreateDefinition("First", firstPrefab);
            secondDefinition = CreateDefinition("Second", secondPrefab);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(poolObject);
            Object.DestroyImmediate(firstPrefab);
            Object.DestroyImmediate(secondPrefab);
            Object.DestroyImmediate(firstDefinition);
            Object.DestroyImmediate(secondDefinition);
        }

        [Test]
        public void Spawn_ReusesViewsOnlyForTheirDefinition()
        {
            EnemyViewPool pool = poolObject.AddComponent<EnemyViewPool>();

            pool.Spawn(Snapshot(1L, firstDefinition));
            EnemyView firstView = FindView(pool, 1L);
            pool.Despawn(1L);

            pool.Spawn(Snapshot(2L, secondDefinition));
            EnemyView secondView = FindView(pool, 2L);
            pool.Despawn(2L);

            pool.Spawn(Snapshot(3L, firstDefinition));
            pool.Spawn(Snapshot(4L, secondDefinition));

            Assert.That(FindView(pool, 3L), Is.SameAs(firstView));
            Assert.That(FindView(pool, 4L), Is.SameAs(secondView));
            Assert.That(firstView, Is.Not.SameAs(secondView));

            pool.ReleaseAll();
            Assert.That(firstView.gameObject.activeSelf, Is.False);
            Assert.That(secondView.gameObject.activeSelf, Is.False);
        }

        private static GameObject CreateViewPrefab(string name)
        {
            var prefab = new GameObject(name);
            AddElementStatusView(prefab);
            prefab.AddComponent<EnemyView>();
            prefab.SetActive(false);
            return prefab;
        }

        private static void AddElementStatusView(GameObject viewObject)
        {
            var statusObject = new GameObject("Element Status");
            statusObject.transform.SetParent(viewObject.transform);
            var iconRoot = new GameObject("Icons");
            iconRoot.transform.SetParent(statusObject.transform);
            Transform fire = CreateIcon(iconRoot.transform, "Fire");
            Transform water = CreateIcon(iconRoot.transform, "Water");
            Transform earth = CreateIcon(iconRoot.transform, "Earth");
            Transform wind = CreateIcon(iconRoot.transform, "Wind");

            EnemyElementStatusView statusView = statusObject.AddComponent<EnemyElementStatusView>();
            var serialized = new SerializedObject(statusView);
            serialized.FindProperty("iconRoot").objectReferenceValue = iconRoot.transform;
            serialized.FindProperty("fireIcon").objectReferenceValue = fire;
            serialized.FindProperty("waterIcon").objectReferenceValue = water;
            serialized.FindProperty("earthIcon").objectReferenceValue = earth;
            serialized.FindProperty("windIcon").objectReferenceValue = wind;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform CreateIcon(Transform parent, string name)
        {
            var icon = new GameObject(name);
            icon.transform.SetParent(parent);
            return icon.transform;
        }

        private static EnemyDefinition CreateDefinition(string name, GameObject viewPrefab)
        {
            EnemyDefinition definition = ScriptableObject.CreateInstance<EnemyDefinition>();
            definition.name = name;
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("displayName").stringValue = name;
            serialized.FindProperty("viewPrefab").objectReferenceValue = viewPrefab;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        private static EnemySnapshot Snapshot(long enemyId, EnemyDefinition definition) =>
            new EnemySnapshot(
                enemyId,
                definition,
                Vector3.zero,
                Vector3.zero,
                definition.BaseMaxHealth,
                false,
                false);

        private static EnemyView FindView(EnemyViewPool pool, long enemyId)
        {
            EnemyView[] views = pool.GetComponentsInChildren<EnemyView>(true);
            for (int index = 0; index < views.Length; index++)
            {
                if (views[index].EnemyId == enemyId)
                {
                    return views[index];
                }
            }

            return null;
        }
    }
}
