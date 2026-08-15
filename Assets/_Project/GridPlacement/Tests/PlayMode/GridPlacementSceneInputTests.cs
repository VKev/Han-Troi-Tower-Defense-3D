using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TowerDefense3D.GridPlacement.Tests.PlayMode
{
    public sealed class GridPlacementSceneInputTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator EditorMouseRelease_PlacesOnceThenRetainsInvalidCandidate()
        {
            yield return SceneManager.LoadSceneAsync(
                "Assets/Scenes/Levels/Level_001.unity",
                LoadSceneMode.Single);
            yield return null;
            yield return null;

            GridPlacementController controller =
                Object.FindFirstObjectByType<GridPlacementController>();
            GameObject placedRoot = GameObject.Find("Grid Placement/Placed Towers");
            GameObject boardOrigin = GameObject.Find("Grid Placement/Board Origin");
            Camera camera = Camera.main;

            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.Occupancy, Is.Not.Null);
            Assert.That(controller.SelectedTower, Is.Not.Null);
            Assert.That(placedRoot, Is.Not.Null);
            Assert.That(boardOrigin, Is.Not.Null);
            Assert.That(placedRoot.transform.childCount, Is.Zero);

            Mouse mouse = InputSystem.AddDevice<Mouse>();
            Vector3 placementPoint = boardOrigin.transform.TransformPoint(
                new Vector3(5.25f, 0f, 1.25f));
            Vector3 screenPoint = camera.WorldToScreenPoint(placementPoint);
            Assert.That(screenPoint.z, Is.GreaterThan(0f));

            Set(mouse.position, new Vector2(screenPoint.x, screenPoint.y));
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);
            yield return null;

            Assert.That(placedRoot.transform.childCount, Is.EqualTo(1));
            Assert.That(controller.HasCandidate, Is.True);
            Assert.That(controller.CandidateIsValid, Is.False);

            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);
            yield return null;

            Assert.That(placedRoot.transform.childCount, Is.EqualTo(1));
            Assert.That(controller.HasCandidate, Is.True);
            Assert.That(controller.CandidateIsValid, Is.False);

            controller.CancelPlacement();
            Assert.That(controller.SelectedTower, Is.Null);
            Assert.That(controller.HasCandidate, Is.False);
        }
    }
}
