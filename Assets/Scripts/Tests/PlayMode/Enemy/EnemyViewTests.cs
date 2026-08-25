using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TowerDefense3D.Enemies.Tests.PlayMode
{
    public sealed class EnemyViewTests
    {
        [UnityTest]
        public IEnumerator Render_MovementOnXZPlaneTurnsViewAndPreservesAuthoredOffset()
        {
            var viewObject = new GameObject("Enemy View");
            viewObject.transform.localRotation = Quaternion.Euler(0f, 35f, 0f);
            Quaternion authoredOffset = viewObject.transform.localRotation;
            EnemyView view = viewObject.AddComponent<EnemyView>();
            yield return null;

            EnemySnapshot enemy = Snapshot(Vector3.zero, new Vector3(1f, 5f, 0f));
            view.Render(enemy, 1f);

            Quaternion expectedRotation = Quaternion.LookRotation(Vector3.right, Vector3.up) * authoredOffset;
            Assert.That(Quaternion.Angle(viewObject.transform.rotation, expectedRotation), Is.LessThan(0.01f));

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
