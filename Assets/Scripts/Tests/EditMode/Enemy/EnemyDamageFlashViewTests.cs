using NUnit.Framework;
using TowerDefense3D.Towers;
using UnityEngine;

namespace TowerDefense3D.Enemies.Tests.EditMode
{
    public sealed class EnemyDamageFlashViewTests
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private GameObject viewObject;
        private EnemyDefinition definition;
        private MeshRenderer renderer;
        private EnemyDamageFlashView damageFlashView;

        [SetUp]
        public void SetUp()
        {
            viewObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            renderer = viewObject.GetComponent<MeshRenderer>();
            damageFlashView = viewObject.AddComponent<EnemyDamageFlashView>();
            definition = ScriptableObject.CreateInstance<EnemyDefinition>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(viewObject);
            Object.DestroyImmediate(definition);
        }

        [Test]
        public void Render_DamageAtFullHealth_FlashesWhite()
        {
            damageFlashView.Bind(CreateSnapshot(definition.BaseMaxHealth));

            damageFlashView.Render(CreateSnapshot(definition.BaseMaxHealth - 1f), 0f);

            Assert.That(GetRenderedColor(), Is.EqualTo(Color.white));
        }

        [Test]
        public void Render_DamageAtHalfHealth_FlashesHalfwayBetweenWhiteAndRed()
        {
            float halfHealth = definition.BaseMaxHealth * 0.5f;
            damageFlashView.Bind(CreateSnapshot(halfHealth));

            damageFlashView.Render(CreateSnapshot(halfHealth - 1f), 0f);

            Assert.That(GetRenderedColor(), Is.EqualTo(new Color(1f, 0.5f, 0.5f, 1f)));
        }

        [Test]
        public void Release_ClearsDamageFlashForPoolReuse()
        {
            damageFlashView.Bind(CreateSnapshot(definition.BaseMaxHealth));
            damageFlashView.Render(CreateSnapshot(definition.BaseMaxHealth - 1f), 0f);

            damageFlashView.Release();

            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            Assert.That(properties.isEmpty, Is.True);
        }

        [Test]
        public void Render_ElementStatusWithoutDamage_DoesNotChangeColor()
        {
            EnemyElementState marked = new EnemyElementState(
                EnemyElementPhase.Marked,
                ElementType.Fire,
                3f);
            damageFlashView.Bind(CreateSnapshot(definition.BaseMaxHealth, marked));

            damageFlashView.Render(CreateSnapshot(definition.BaseMaxHealth, marked), 0f);

            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            Assert.That(properties.isEmpty, Is.True);
        }

        private EnemySnapshot CreateSnapshot(float health, EnemyElementState elementState = default)
        {
            return new EnemySnapshot(
                1L,
                definition,
                Vector3.zero,
                Vector3.zero,
                health,
                false,
                false,
                elementState);
        }

        private Color GetRenderedColor()
        {
            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            return properties.GetColor(BaseColor);
        }
    }
}
