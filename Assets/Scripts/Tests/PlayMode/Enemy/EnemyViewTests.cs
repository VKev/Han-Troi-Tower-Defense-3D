using System.Collections;
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

        private static EnemySnapshot Snapshot(Vector3 previousPosition, Vector3 position) =>
            new EnemySnapshot(
                1L,
                null,
                previousPosition,
                position,
                1f,
                false,
                false);
    }
}
