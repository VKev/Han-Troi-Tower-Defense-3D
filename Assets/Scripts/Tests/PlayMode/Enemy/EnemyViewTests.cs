using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TowerDefense3D.Enemies.Tests.PlayMode
{
    public sealed class EnemyViewTests
    {
        [UnityTest]
        public IEnumerator Render_FirstMovementFacesTravelDirection()
        {
            var viewObject = new GameObject("Enemy View");
            viewObject.transform.localRotation = Quaternion.Euler(0f, 175f, 0f);
            AddElementStatusView(viewObject);
            EnemyView view = viewObject.AddComponent<EnemyView>();
            yield return null;

            EnemySnapshot enemy = Snapshot(Vector3.zero, new Vector3(1f, 5f, 0f));
            view.Render(enemy, 1f);

            Quaternion expectedRotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
            Assert.That(Quaternion.Angle(viewObject.transform.rotation, expectedRotation), Is.LessThan(0.01f));

            Object.Destroy(viewObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Render_DirectionChangeTurnsGradually()
        {
            var viewObject = new GameObject("Enemy View");
            AddElementStatusView(viewObject);
            EnemyView view = viewObject.AddComponent<EnemyView>();
            yield return null;

            view.Render(Snapshot(Vector3.zero, Vector3.forward), 1f);
            Quaternion initialRotation = viewObject.transform.rotation;
            Quaternion targetRotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
            float fullTurnAngle = Quaternion.Angle(initialRotation, targetRotation);
            yield return null;

            view.Render(Snapshot(Vector3.zero, Vector3.right), 1f);

            float completedTurnAngle = Quaternion.Angle(initialRotation, viewObject.transform.rotation);
            float remainingTurnAngle = Quaternion.Angle(viewObject.transform.rotation, targetRotation);
            Assert.That(completedTurnAngle, Is.GreaterThan(0f));
            Assert.That(completedTurnAngle, Is.LessThan(fullTurnAngle));
            Assert.That(remainingTurnAngle, Is.GreaterThan(0f));

            Object.Destroy(viewObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Render_NoMovementOnXZPlaneKeepsCurrentRotation()
        {
            var viewObject = new GameObject("Enemy View");
            AddElementStatusView(viewObject);
            EnemyView view = viewObject.AddComponent<EnemyView>();
            yield return null;

            viewObject.transform.rotation = Quaternion.Euler(0f, 120f, 0f);
            Quaternion expectedRotation = viewObject.transform.rotation;
            EnemySnapshot enemy = Snapshot(Vector3.zero, new Vector3(0f, 5f, 0f));
            view.Render(enemy, 1f);

            Assert.That(Quaternion.Angle(viewObject.transform.rotation, expectedRotation), Is.LessThan(0.01f));

            Object.Destroy(viewObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BindAndDeathScale_InterpolatesAroundFootPivot()
        {
            var viewObject = new GameObject("Enemy View");
            AddElementStatusView(viewObject);
            var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            model.name = "Model";
            model.transform.SetParent(viewObject.transform);
            model.transform.localPosition = new Vector3(0f, 1f, 0f);
            model.transform.localScale = new Vector3(1f, 2f, 1f);
            EnemyView view = viewObject.AddComponent<EnemyView>();
            EnemyDefinition definition = ScriptableObject.CreateInstance<EnemyDefinition>();
            yield return null;

            view.Bind(new EnemySnapshot(
                1L,
                definition,
                new Vector3(3f, 2f, 4f),
                new Vector3(3f, 2f, 4f),
                definition.BaseMaxHealth,
                false,
                false));

            Renderer modelRenderer = model.GetComponent<Renderer>();
            float footY = modelRenderer.bounds.min.y;
            view.TickLifecycle(0.2f);
            Assert.That(viewObject.transform.localScale.x, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(modelRenderer.bounds.min.y, Is.EqualTo(footY).Within(0.001f));

            view.TickLifecycle(0.2f);
            Assert.That(viewObject.transform.localScale.x, Is.EqualTo(1f).Within(0.001f));
            Assert.That(modelRenderer.bounds.min.y, Is.EqualTo(footY).Within(0.001f));

            bool completed = false;
            view.BeginDeath(() => completed = true);
            view.TickLifecycle(0.1f);
            Assert.That(completed, Is.False);
            Assert.That(viewObject.transform.localScale.x, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(modelRenderer.bounds.min.y, Is.EqualTo(footY).Within(0.001f));

            view.TickLifecycle(0.1f);
            Assert.That(completed, Is.True);
            Assert.That(viewObject.transform.localScale.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(modelRenderer.bounds.min.y, Is.EqualTo(footY).Within(0.001f));

            Object.Destroy(viewObject);
            Object.Destroy(definition);
            yield return null;
        }

        private static EnemySnapshot Snapshot(Vector3 previousPosition, Vector3 position) =>
            new EnemySnapshot(
                1L,
                null,
                previousPosition,
                position,
                1f,
                false,
                false);

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
            SetField(statusView, "iconRoot", iconRoot.transform);
            SetField(statusView, "fireIcon", fire);
            SetField(statusView, "waterIcon", water);
            SetField(statusView, "windIcon", wind);
            iconRoot.SetActive(false);

            AddElementEffectView(viewObject);
        }

        private static void AddElementEffectView(GameObject viewObject)
        {
            var effectObject = new GameObject("Element Effect");
            effectObject.transform.SetParent(viewObject.transform);
            var fireObject = new GameObject("Fire");
            fireObject.transform.SetParent(effectObject.transform);
            fireObject.AddComponent<ParticleSystem>();
            fireObject.SetActive(false);

            EnemyElementEffectView effectView = effectObject.AddComponent<EnemyElementEffectView>();
            typeof(EnemyElementEffectView)
                .GetField("fireEffect", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(effectView, fireObject);
        }

        private static Transform CreateIcon(Transform parent, string name)
        {
            var icon = new GameObject(name);
            icon.transform.SetParent(parent);
            return icon.transform;
        }

        private static void SetField(EnemyElementStatusView view, string name, Transform value)
        {
            typeof(EnemyElementStatusView)
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(view, value);
        }
    }
}
