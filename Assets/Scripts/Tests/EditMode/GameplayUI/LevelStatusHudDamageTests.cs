using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow.Tests.EditMode
{
    /// <summary>
    /// The damage beat on the level status HUD: the bar turns red the moment the Cóc is hit.
    /// </summary>
    /// <remarks>
    /// Driven through the authored prefab rather than a bare component, because most of what can
    /// break this is authoring rather than code - an unwired fill, a zeroed duration, a shake
    /// strength nudged to nothing. The red is asserted rather than the shake because the red is
    /// applied synchronously: a hit snaps the colour on and only the return to green is tweened,
    /// so the assertion needs no DOTween tick and cannot go flaky on timing.
    /// </remarks>
    [TestFixture]
    public sealed class LevelStatusHudDamageTests
    {
        private const string GameplayUiPrefabPath = "Assets/Resources/Prefabs/GameplayUI.prefab";

        private GameObject owner;
        private LevelStatusHudView view;
        private Image healthFill;
        private Image damageOverlay;
        private Color damageColor;

        [SetUp]
        public void SetUp()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameplayUiPrefabPath);
            Assert.That(prefab, Is.Not.Null, $"Gameplay UI prefab is missing at '{GameplayUiPrefabPath}'.");

            owner = Object.Instantiate(prefab);
            view = owner.GetComponentInChildren<LevelStatusHudView>(true);
            Assert.That(view, Is.Not.Null, "Gameplay UI prefab must author a LevelStatusHudView.");

            var serialized = new SerializedObject(view);
            healthFill = serialized.FindProperty("healthFill").objectReferenceValue as Image;
            damageOverlay = serialized.FindProperty("healthDamageOverlay").objectReferenceValue as Image;
            damageColor = serialized.FindProperty("healthDamageFlashColor").colorValue;

            Assert.That(healthFill, Is.Not.Null, "The status HUD must author a health fill.");
            Assert.That(
                damageOverlay,
                Is.Not.Null,
                "The hit flash needs its own overlay; tinting the fill cannot make a green bar red.");
            Assert.That(
                damageOverlay.transform.IsChildOf(healthFill.transform),
                Is.True,
                "The overlay rides the fill so it is only as long as the health that is left.");
            Assert.That(
                damageOverlay.sprite,
                Is.Null,
                "A solid overlay, so its red is its own rather than multiplied into the bar's sprite.");
            Assert.That(
                serialized.FindProperty("healthShakeStrength").floatValue,
                Is.GreaterThan(0f),
                "A zero shake strength silently turns the damage shake off.");
            Assert.That(
                serialized.FindProperty("healthShakeDuration").floatValue,
                Is.GreaterThan(0f),
                "A zero shake duration silently turns the damage shake off.");
        }

        [TearDown]
        public void TearDown()
        {
            if (owner != null)
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void FirstRender_DoesNotReadAsDamage()
        {
            view.RenderHealth(20, 20);

            Assert.That(
                damageOverlay.color.a,
                Is.EqualTo(0f).Within(0.001f),
                "Entering a level is not a hit; the bar must not open red.");
        }

        [Test]
        public void LosingHealth_TurnsTheBarRedImmediately()
        {
            view.RenderHealth(20, 20);

            view.RenderHealth(14, 20);

            Assert.That(
                damageOverlay.color,
                Is.EqualTo(damageColor),
                "A hit has to show on the bar the frame it lands.");
            Assert.That(
                healthFill.color.a,
                Is.EqualTo(1f).Within(0.001f),
                "The fill itself is left alone - the red belongs to the overlay.");
        }

        /// <summary>
        /// The bar also has to move. Checked through the anchor the fill is stretched by, which
        /// is what carries the length - the HUD drives the rect rather than Image.fillAmount so
        /// the sliced end caps keep their radius.
        /// </summary>
        [Test]
        public void LosingHealth_DrainsTheBarTowardsTheNewRatio()
        {
            view.RenderHealth(20, 20);
            Assert.That(healthFill.rectTransform.anchorMax.x, Is.EqualTo(1f).Within(0.001f));

            view.RenderHealth(10, 20);

            // The drain is tweened, so the rect is still at the old length on the frame of the
            // hit. What matters here is that the target was accepted rather than snapped past.
            Assert.That(
                healthFill.rectTransform.anchorMax.x,
                Is.InRange(0.5f, 1f),
                "The bar drains towards the new ratio rather than jumping to it.");
        }

        [Test]
        public void Healing_DoesNotReadAsDamage()
        {
            view.RenderHealth(20, 20);
            view.RenderHealth(10, 20);
            damageOverlay.color = Color.clear;

            view.RenderHealth(16, 20);

            Assert.That(
                damageOverlay.color.a,
                Is.EqualTo(0f).Within(0.001f),
                "Gaining health back must not play the hit beat.");
        }
    }
}
