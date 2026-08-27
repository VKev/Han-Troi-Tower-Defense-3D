using NUnit.Framework;
using TowerDefense3D.Towers;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.Enemies.Tests.EditMode
{
    public sealed class EnemyElementEffectViewTests
    {
        private static readonly Vector3 AuthoredScale = new Vector3(2f, 3f, 4f);

        private GameObject viewObject;
        private GameObject fireRoot;
        private ParticleSystem fire;
        private ParticleSystem smoke;
        private EnemyElementEffectView view;

        [SetUp]
        public void SetUp()
        {
            viewObject = new GameObject("Element Effect View");
            fireRoot = CreateEffectRoot("Fire");
            fire = CreateParticleSystem(fireRoot.transform, "Flame");
            smoke = CreateParticleSystem(fireRoot.transform, "Smoke");
            fireRoot.transform.localScale = AuthoredScale;
            fireRoot.SetActive(false);
            view = viewObject.AddComponent<EnemyElementEffectView>();

            var serialized = new SerializedObject(view);
            serialized.FindProperty("fireEffect").objectReferenceValue = fireRoot;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(viewObject);
        }

        [Test]
        public void Bind_FireMark_ActivatesFireRootAtZeroScale()
        {
            view.Bind(MarkedWith(ElementType.Fire));

            Assert.That(fireRoot.activeSelf, Is.True);
            Assert.That(fireRoot.transform.localScale, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Render_FireMark_ScalesFireRootToAuthoredScaleInPointTwoSeconds()
        {
            view.Bind(MarkedWith(ElementType.Fire));

            view.Render(MarkedWith(ElementType.Fire), 0.1f);
            Assert.That(fireRoot.transform.localScale, Is.EqualTo(AuthoredScale * 0.5f));

            view.Render(MarkedWith(ElementType.Fire), 0.1f);
            Assert.That(fireRoot.transform.localScale, Is.EqualTo(AuthoredScale));
        }

        [Test]
        public void Render_ClearingMark_ScalesOutThenDeactivatesFireRoot()
        {
            view.Bind(MarkedWith(ElementType.Fire));
            view.Render(MarkedWith(ElementType.Fire), 0.2f);

            var ready = new EnemyElementState(EnemyElementPhase.Ready, default, 0f);
            view.Render(ready, 0.1f);
            Assert.That(fireRoot.activeSelf, Is.True);
            Assert.That(fireRoot.transform.localScale, Is.EqualTo(AuthoredScale * 0.5f));

            view.Render(ready, 0.1f);
            Assert.That(fireRoot.activeSelf, Is.False);
            Assert.That(fireRoot.transform.localScale, Is.EqualTo(AuthoredScale));
        }

        [Test]
        public void Render_ElementWithoutAuthoredEffect_PlaysNothing()
        {
            view.Render(MarkedWith(ElementType.Water), 0.2f);

            Assert.That(fireRoot.activeSelf, Is.False);
        }

        [Test]
        public void Render_SameMarkTwice_DoesNotRestartScaleTransition()
        {
            view.Bind(MarkedWith(ElementType.Fire));
            view.Render(MarkedWith(ElementType.Fire), 0.1f);
            Vector3 scaleBefore = fireRoot.transform.localScale;

            view.Render(MarkedWith(ElementType.Fire), 0f);

            Assert.That(fireRoot.transform.localScale, Is.EqualTo(scaleBefore));
        }

        [Test]
        public void Release_StopsNestedParticlesAndResetsRootForPoolReuse()
        {
            view.Bind(MarkedWith(ElementType.Fire));

            view.Release();

            Assert.That(fireRoot.activeSelf, Is.False);
            Assert.That(fire.isPlaying, Is.False);
            Assert.That(fire.particleCount, Is.Zero);
            Assert.That(smoke.isPlaying, Is.False);
            Assert.That(smoke.particleCount, Is.Zero);
            Assert.That(fireRoot.transform.localScale, Is.EqualTo(AuthoredScale));
        }

        private static EnemyElementState MarkedWith(ElementType element)
        {
            return new EnemyElementState(EnemyElementPhase.Marked, element, 3f);
        }

        private GameObject CreateEffectRoot(string name)
        {
            var effectRoot = new GameObject(name);
            effectRoot.transform.SetParent(viewObject.transform);
            return effectRoot;
        }

        private static ParticleSystem CreateParticleSystem(Transform parent, string name)
        {
            var particleObject = new GameObject(name);
            particleObject.transform.SetParent(parent);
            return particleObject.AddComponent<ParticleSystem>();
        }
    }
}
