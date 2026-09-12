using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TowerDefense3D.GridPlacement.Tests.PlayMode
{
    public sealed class GridPlacementViewTests
    {
        /// <summary>
        /// The preview is authored, not built: it comes from PlacementPreview.prefab.
        /// </summary>
        /// <remarks>
        /// This test used to bolt the view onto a bare GameObject and let it construct its own
        /// Footprint and GhostVolume children. It does not construct them any more - they are
        /// authored in the prefab and the view refuses to run without them - so the bare-object
        /// setup got the refusal it asks for, as an unhandled MissingReferenceException.
        ///
        /// What is still worth holding is unchanged: the two meshes are built once and reused
        /// across moves rather than rebuilt per frame, the ghost is kept but never drawn, and
        /// hiding switches the footprint off.
        /// </remarks>
        [UnityTest]
        public IEnumerator View_ReusesCombinedRenderersAndHidesCleanly()
        {
            var prefab = Resources.Load<GameObject>("Prefabs/PlacementPreview");
            Assert.That(prefab, Is.Not.Null, "PlacementPreview.prefab is missing from Resources.");
            GameObject root = Object.Instantiate(prefab);
            GridPlacementView view = root.GetComponent<GridPlacementView>();
            Assert.That(view, Is.Not.Null, "PlacementPreview must author a GridPlacementView.");

            // Counted before anything is shown, so the assertion below is about what Show adds
            // rather than about how many pieces the preview happens to be authored from.
            int authoredRendererCount = root.GetComponentsInChildren<MeshRenderer>(true).Length;
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
            MeshFilter ghost = root.transform.Find("Ghost Volume").GetComponent<MeshFilter>();
            MeshRenderer footprintRenderer = footprint.GetComponent<MeshRenderer>();
            MeshRenderer ghostRenderer = ghost.GetComponent<MeshRenderer>();
            Mesh footprintMesh = footprint.sharedMesh;
            Mesh ghostMesh = ghost.sharedMesh;

            Assert.That(
                root.GetComponentsInChildren<MeshRenderer>(true),
                Has.Length.EqualTo(authoredRendererCount),
                "A footprint is one combined mesh, not a renderer per cell - showing one must "
                + "add no renderers at all.");
            Assert.That(footprintMesh.vertexCount, Is.EqualTo(24));
            Assert.That(ghostMesh.vertexCount, Is.EqualTo(8));

            Assert.That(footprintRenderer.enabled, Is.True);
            Assert.That(
                ghostRenderer.enabled,
                Is.False,
                "The ghost volume is built and kept but never drawn - a translucent box over the "
                + "board reads as a tower already placed rather than as a target.");
            view.Show(
                new TowerFootprint(2, 3, 2),
                new Vector3(6f, 1f, 7f),
                1f,
                1f,
                12f,
                false);
            Assert.That(footprint.sharedMesh, Is.SameAs(footprintMesh));
            Assert.That(ghost.sharedMesh, Is.SameAs(ghostMesh));

            view.Show(
                new TowerFootprint(2, 3, 2),
                new Vector3(6f, 1f, 7f),
                1f,
                1f,
                12f,
                true);
            view.Hide();
            Assert.That(footprintRenderer.enabled, Is.False);

            Object.Destroy(root);
            yield return null;
        }
    }
}
