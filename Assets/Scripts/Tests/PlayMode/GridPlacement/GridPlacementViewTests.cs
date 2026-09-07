using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TowerDefense3D.GridPlacement.Tests.PlayMode
{
    public sealed class GridPlacementViewTests
    {
        [UnityTest]
        public IEnumerator View_ReusesCombinedRenderersAndHidesCleanly()
        {
            var root = new GameObject("View Test");
            GridPlacementView view = root.AddComponent<GridPlacementView>();
            yield return null;

            view.Show(
                new TowerFootprint(2, 3, 2),
                new Vector3(5f, 1f, 6f),
                1f,
                1f,
                12f,
                true);
            yield return null;

            MeshFilter footprint = root.transform.Find("Footprint").GetComponent<MeshFilter>();
            MeshFilter ghost = root.transform.Find("GhostVolume").GetComponent<MeshFilter>();
            MeshFilter linkRange = root.transform.Find("LinkRange").GetComponent<MeshFilter>();
            MeshRenderer footprintRenderer = footprint.GetComponent<MeshRenderer>();
            MeshRenderer ghostRenderer = ghost.GetComponent<MeshRenderer>();
            MeshRenderer linkRangeRenderer = linkRange.GetComponent<MeshRenderer>();
            Mesh footprintMesh = footprint.sharedMesh;
            Mesh ghostMesh = ghost.sharedMesh;
            Mesh linkRangeMesh = linkRange.sharedMesh;

            Assert.That(root.GetComponentsInChildren<MeshRenderer>(true), Has.Length.EqualTo(3));
            Assert.That(footprintMesh.vertexCount, Is.EqualTo(24));
            Assert.That(ghostMesh.vertexCount, Is.EqualTo(8));
            Assert.That(
                linkRangeMesh.vertexCount,
                Is.EqualTo(192),
                "The ring is an annulus: two vertices per segment, ninety-six segments.");

            Assert.That(footprintRenderer.enabled, Is.True);
            Assert.That(
                ghostRenderer.enabled,
                Is.False,
                "The ghost volume is built and kept but never drawn - a translucent box over the "
                + "board reads as a tower already placed rather than as a target.");
            Assert.That(linkRangeRenderer.enabled, Is.True);

            view.Show(
                new TowerFootprint(2, 3, 2),
                new Vector3(6f, 1f, 7f),
                1f,
                1f,
                12f,
                false);
            Assert.That(footprint.sharedMesh, Is.SameAs(footprintMesh));
            Assert.That(ghost.sharedMesh, Is.SameAs(ghostMesh));
            Assert.That(linkRange.sharedMesh, Is.SameAs(linkRangeMesh));

            // A caller with no network rules to consult passes no range, and gets no ring.
            view.Show(
                new TowerFootprint(2, 3, 2),
                new Vector3(6f, 1f, 7f),
                1f,
                1f,
                0f,
                true);
            Assert.That(linkRangeRenderer.enabled, Is.False);

            view.Show(
                new TowerFootprint(2, 3, 2),
                new Vector3(6f, 1f, 7f),
                1f,
                1f,
                12f,
                true);
            view.Hide();
            Assert.That(footprintRenderer.enabled, Is.False);
            Assert.That(linkRangeRenderer.enabled, Is.False);

            Object.Destroy(root);
            yield return null;
        }
    }
}
