using NUnit.Framework;
using TowerDefense3D.Towers;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.Enemies.Tests.EditMode
{
    public sealed class EnemyElementStatusViewTests
    {
        private GameObject viewObject;
        private GameObject iconRoot;
        private Transform fire;
        private Transform water;
        private Transform earth;
        private Transform wind;
        private EnemyElementStatusView view;

        [SetUp]
        public void SetUp()
        {
            viewObject = new GameObject("Element Status View");
            iconRoot = new GameObject("Icons");
            iconRoot.transform.SetParent(viewObject.transform);
            fire = CreateIcon("Fire");
            water = CreateIcon("Water");
            earth = CreateIcon("Earth");
            wind = CreateIcon("Wind");
            view = viewObject.AddComponent<EnemyElementStatusView>();

            var serialized = new SerializedObject(view);
            serialized.FindProperty("iconRoot").objectReferenceValue = iconRoot.transform;
            serialized.FindProperty("fireIcon").objectReferenceValue = fire;
            serialized.FindProperty("waterIcon").objectReferenceValue = water;
            serialized.FindProperty("earthIcon").objectReferenceValue = earth;
            serialized.FindProperty("windIcon").objectReferenceValue = wind;
            serialized.FindProperty("reactionDisplaySeconds").floatValue = 0.2f;
            serialized.FindProperty("reactionIconSpacing").floatValue = 0.6f;
            serialized.FindProperty("iconWorldScale").floatValue = 0.55f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(viewObject);
        }

        [Test]
        public void Bind_Marked_ShowsOneMatchingIconAtCenter()
        {
            view.Bind(new EnemyElementState(
                EnemyElementPhase.Marked,
                ElementType.Water,
                3f));

            Assert.That(iconRoot.activeSelf, Is.True);
            Assert.That(water.gameObject.activeSelf, Is.True);
            Assert.That(water.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(fire.gameObject.activeSelf, Is.False);
            Assert.That(earth.gameObject.activeSelf, Is.False);
            Assert.That(wind.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void ShowReaction_ShowsCenteredPairThenHidesAfterDelay()
        {
            view.ShowReaction(new ElementPair(ElementType.Earth, ElementType.Fire));

            Assert.That(iconRoot.activeSelf, Is.True);
            Assert.That(fire.gameObject.activeSelf, Is.True);
            Assert.That(earth.gameObject.activeSelf, Is.True);
            Assert.That(fire.localPosition.x, Is.EqualTo(-0.3f).Within(0.0001f));
            Assert.That(earth.localPosition.x, Is.EqualTo(0.3f).Within(0.0001f));

            view.Render(default, 0.19f);
            Assert.That(iconRoot.activeSelf, Is.True);

            view.Render(default, 0.02f);
            Assert.That(iconRoot.activeSelf, Is.False);
        }

        [Test]
        public void ShowReaction_SameElement_ShowsTwoIconsSideBySide()
        {
            view.ShowReaction(new ElementPair(ElementType.Fire, ElementType.Fire));

            Assert.That(iconRoot.activeSelf, Is.True);
            Assert.That(fire.gameObject.activeSelf, Is.True);
            Assert.That(fire.localPosition.x, Is.EqualTo(-0.3f).Within(0.0001f));

            // The second copy is a borrowed quad re-pointed at the same icon mesh.
            Assert.That(water.gameObject.activeSelf, Is.True);
            Assert.That(water.localPosition.x, Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(earth.gameObject.activeSelf, Is.False);
            Assert.That(wind.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void Configure_KeepsIconSizeIndependentOfEnemyScale()
        {
            viewObject.transform.localScale = new Vector3(2f, 2f, 2f);

            view.Configure(null);

            Assert.That(iconRoot.transform.lossyScale.x, Is.EqualTo(0.55f).Within(0.0001f));
            Assert.That(iconRoot.transform.lossyScale.y, Is.EqualTo(0.55f).Within(0.0001f));
            Assert.That(iconRoot.transform.lossyScale.z, Is.EqualTo(0.55f).Within(0.0001f));
        }

        [Test]
        public void Release_HidesStatusForPoolReuse()
        {
            view.ShowReaction(new ElementPair(ElementType.Water, ElementType.Wind));

            view.Release();

            Assert.That(iconRoot.activeSelf, Is.False);
            Assert.That(fire.gameObject.activeSelf, Is.False);
            Assert.That(water.gameObject.activeSelf, Is.False);
            Assert.That(earth.gameObject.activeSelf, Is.False);
            Assert.That(wind.gameObject.activeSelf, Is.False);
        }

        private Transform CreateIcon(string name)
        {
            var icon = new GameObject(name);
            icon.transform.SetParent(iconRoot.transform);
            return icon.transform;
        }
    }
}
