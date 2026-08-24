using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace TowerDefense3D.GridPlacement.Tests.PlayMode
{
    public sealed class BoardColliderPlacementTests
    {
        [Test]
        public void ApplyVisibility_WhenVisualizationIsDisabled_KeepsColliderRaycastable()
        {
            BoardDefinition board = ScriptableObject.CreateInstance<BoardDefinition>();
            GameObject boardRoot = new GameObject("Board Root");
            GameObject viewObject = new GameObject("Board View");

            try
            {
                SetPrivateField(board, "visualizeInScene", false);

                GameObject cell = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cell.name = "Board Cell";
                cell.transform.SetParent(boardRoot.transform, false);

                MeshRenderer renderer = cell.GetComponent<MeshRenderer>();
                BoxCollider collider = cell.GetComponent<BoxCollider>();
                Assert.That(renderer.enabled, Is.True);
                Assert.That(collider.enabled, Is.True);

                BoardView view = viewObject.AddComponent<BoardView>();
                SetPrivateField(view, "board", board);
                SetPrivateField(view, "generatedRoot", boardRoot.transform);

                var boardSystem = new BoardSystem(view);
                boardSystem.Start();

                Assert.That(renderer.enabled, Is.False);
                Assert.That(collider.enabled, Is.True);

                Physics.SyncTransforms();
                bool didHit = Physics.Raycast(Vector3.up * 2f, Vector3.down, out RaycastHit hit, 4f);

                Assert.That(didHit, Is.True);
                Assert.That(hit.collider, Is.SameAs(collider));
            }
            finally
            {
                Object.DestroyImmediate(viewObject);
                Object.DestroyImmediate(boardRoot);
                Object.DestroyImmediate(board);
            }
        }

        private static void SetPrivateField<TTarget>(TTarget target, string fieldName, object value)
        {
            FieldInfo field = typeof(TTarget).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Expected private field '{fieldName}' on {typeof(TTarget).Name}.");
            field.SetValue(target, value);
        }
    }
}
