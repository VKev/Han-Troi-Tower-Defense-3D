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

            EnemySnapshot first = Snapshot(1L, firstDefinition);
            pool.Spawn(first);
            pool.Render(new[] { first }, 1f);
            pool.Render(new[] { first }, 1f);
            EnemyView firstView = FindView(pool, 1L);
            firstView.TickLifecycle(0.4f);
            pool.Despawn(1L);
            pool.TickLifecycle(0.4f);

            EnemySnapshot second = Snapshot(2L, secondDefinition);
            pool.Spawn(second);
            pool.Render(new[] { second }, 1f);
            pool.Render(new[] { second }, 1f);
            EnemyView secondView = FindView(pool, 2L);
            secondView.TickLifecycle(0.4f);
            pool.Despawn(2L);
            pool.TickLifecycle(0.4f);

            pool.Spawn(Snapshot(3L, firstDefinition));
            pool.Spawn(Snapshot(4L, secondDefinition));

            Assert.That(FindView(pool, 3L), Is.SameAs(firstView));
            Assert.That(FindView(pool, 4L), Is.SameAs(secondView));
            Assert.That(firstView, Is.Not.SameAs(secondView));

            pool.ReleaseAll();
            Assert.That(firstView.gameObject.activeSelf, Is.False);
            Assert.That(secondView.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void Spawn_HidesViewUntilFirstRenderAtItsGameplayPosition()
        {
            EnemyViewPool pool = poolObject.AddComponent<EnemyViewPool>();
            Vector3 position = new Vector3(3f, 2f, 4f);
            EnemySnapshot enemy = Snapshot(1L, firstDefinition, position);

            pool.Spawn(enemy);
            EnemyView view = FindView(pool, 1L);
            ParticleSystemRenderer renderer = view.GetComponentInChildren<ParticleSystemRenderer>(true);

            Assert.That(view.gameObject.activeSelf, Is.False);
            Assert.That(renderer.forceRenderingOff, Is.True);

            pool.Render(new[] { enemy }, 1f);

            // The first render moves the view onto its gameplay position and wakes it up, but
            // rendering stays off: the enemy has to stay unseen until it pops with the spawn
            // effect, and being switched on early is what lets it animate under cover.
            Assert.That(renderer.forceRenderingOff, Is.True);
            Assert.That(view.transform.position, Is.EqualTo(position));

            pool.Render(new[] { enemy }, 1f);

            Assert.That(renderer.forceRenderingOff, Is.False);
            Assert.That(view.transform.position, Is.EqualTo(position));
        }

        [Test]
        public void Despawn_KeepsViewActiveUntilScaleAnimationCompletes()
        {
            EnemyViewPool pool = poolObject.AddComponent<EnemyViewPool>();

            EnemySnapshot enemy = Snapshot(1L, firstDefinition);
            pool.Spawn(enemy);
            pool.Render(new[] { enemy }, 1f);
            pool.Render(new[] { enemy }, 1f);
            EnemyView view = FindView(pool, 1L);
            pool.TickLifecycle(1.6f);
            view.TickLifecycle(0.4f);

            pool.Despawn(1L);

            Assert.That(view.gameObject.activeSelf, Is.True);
            pool.TickLifecycle(0.1f);
            Assert.That(view.gameObject.activeSelf, Is.True);

            pool.TickLifecycle(0.1f);
            Assert.That(view.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void Render_KeepsAKnockedBackEnemyOnItsFeet()
        {
            EnemyViewPool pool = poolObject.AddComponent<EnemyViewPool>();
            EnemySnapshot resting = Snapshot(1L, firstDefinition, Vector3.zero);
            pool.Spawn(resting);
            pool.Render(new[] { resting }, 1f);
            pool.Render(new[] { resting }, 1f);

            // A wind tower knocks the enemy straight back down the road it walked up, so its
            // direction of travel reverses exactly. Turning by the rotation that carries the old
            // forward onto the new one has no defined axis at exactly 180 degrees, and the
            // horizontal one Unity picks lays the enemy on its back for good.
            var knockedBack = new EnemySnapshot(
                1L,
                firstDefinition,
                Vector3.forward,
                Vector3.zero,
                firstDefinition.BaseMaxHealth,
                false,
                false);
            pool.Render(new[] { knockedBack }, 1f);

            EnemyView view = FindView(pool, 1L);
            Assert.That(
                Vector3.Angle(view.transform.up, Vector3.up),
                Is.LessThan(0.01f),
                "A knocked back enemy has to stay on its feet, not be laid on its back.");
        }

        private static GameObject CreateViewPrefab(string name)
        {
            var prefab = new GameObject(name);
            AddElementStatusView(prefab);
            AddElementEffectView(prefab);
            prefab.AddComponent<EnemyView>();
            prefab.SetActive(false);
            return prefab;
        }

        private static void AddElementEffectView(GameObject viewObject)
        {
            var effectObject = new GameObject("Element Effect");
            effectObject.transform.SetParent(viewObject.transform);
            var fireRoot = new GameObject("Fire");
            fireRoot.transform.SetParent(effectObject.transform);
            fireRoot.AddComponent<ParticleSystem>();
            fireRoot.SetActive(false);

            EnemyElementEffectView effectView = effectObject.AddComponent<EnemyElementEffectView>();
            var serialized = new SerializedObject(effectView);
            serialized.FindProperty("fireEffect").objectReferenceValue = fireRoot;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddElementStatusView(GameObject viewObject)
        {
            var statusObject = new GameObject("Element Status");
            statusObject.transform.SetParent(viewObject.transform);
            var iconRoot = new GameObject("Icons");
            iconRoot.transform.SetParent(statusObject.transform);
            Transform fire = CreateIcon(iconRoot.transform, "Fire");
            Transform water = CreateIcon(iconRoot.transform, "Water");
            Transform wind = CreateIcon(iconRoot.transform, "Wind");

            EnemyElementStatusView statusView = statusObject.AddComponent<EnemyElementStatusView>();
            var serialized = new SerializedObject(statusView);
            serialized.FindProperty("iconRoot").objectReferenceValue = iconRoot.transform;
            serialized.FindProperty("fireIcon").objectReferenceValue = fire;
            serialized.FindProperty("waterIcon").objectReferenceValue = water;
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
            Snapshot(enemyId, definition, Vector3.zero);

        private static EnemySnapshot Snapshot(
            long enemyId,
            EnemyDefinition definition,
            Vector3 position) =>
            new EnemySnapshot(
                enemyId,
                definition,
                position,
                position,
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
