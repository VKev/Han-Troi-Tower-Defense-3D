using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TowerDefense3D.GridPlacement.Tests.PlayMode
{
    public sealed class GridPlacementViewTests
    {
        [UnityTest]
        public IEnumerator View_ReusesTwoCombinedRenderersAndHidesCleanly()
        {
            var root = new GameObject("View Test");
            GridPlacementView view = root.AddComponent<GridPlacementView>();
            TowerDefinition tower = ScriptableObject.CreateInstance<TowerDefinition>();
            yield return null;

            view.SetTower(tower);
            view.Show(
                new GridCell(3, 4, 1),
                new TowerFootprint(2, 3, 2),
                new Vector3(5f, 1f, 6f),
                1f,
                1f,
                true);
            yield return null;

            MeshFilter footprint = root.transform.Find("Footprint").GetComponent<MeshFilter>();
            MeshFilter ghost = root.transform.Find("GhostVolume").GetComponent<MeshFilter>();
            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            Mesh footprintMesh = footprint.sharedMesh;
            Mesh ghostMesh = ghost.sharedMesh;

            Assert.That(renderers, Has.Length.EqualTo(2));
            Assert.That(footprintMesh.vertexCount, Is.EqualTo(24));
            Assert.That(ghostMesh.vertexCount, Is.EqualTo(8));
            Assert.That(renderers[0].enabled, Is.True);
            Assert.That(renderers[1].enabled, Is.True);

            view.Show(
                new GridCell(4, 5, 1),
                new TowerFootprint(2, 3, 2),
                new Vector3(6f, 1f, 7f),
                1f,
                1f,
                false);
            Assert.That(footprint.sharedMesh, Is.SameAs(footprintMesh));
            Assert.That(ghost.sharedMesh, Is.SameAs(ghostMesh));

            view.Hide();
            Assert.That(renderers[0].enabled, Is.False);
            Assert.That(renderers[1].enabled, Is.False);

            Object.Destroy(root);
            Object.Destroy(tower);
            yield return null;
        }
    }
}
