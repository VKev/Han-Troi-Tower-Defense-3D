using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
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
            BoardScenePresenter presenter =
                boardOrigin != null
                    ? boardOrigin.GetComponent<BoardScenePresenter>()
                    : null;

            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.Occupancy, Is.Not.Null);
            Assert.That(controller.SelectedTower, Is.Not.Null);
            Assert.That(placedRoot, Is.Not.Null);
            Assert.That(boardOrigin, Is.Not.Null);
            Assert.That(presenter, Is.Not.Null);
            Assert.That(camera, Is.Not.Null);
            Assert.That(placedRoot.transform.childCount, Is.Zero);

            Mouse mouse = InputSystem.AddDevice<Mouse>();
            Assert.That(
                TryFindValidPlacementScreenPoint(
                    presenter,
                    controller,
                    camera,
                    out Vector2 screenPoint),
                Is.True,
                "The authored level needs one visible, non-UI placement point "
                + "for the selected tower.");

            Set(mouse.position, screenPoint);
            Press(mouse.leftButton);
            yield return null;

            Assert.That(controller.HasCandidate, Is.True);
            Assert.That(controller.CandidateIsValid, Is.True);

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

        private static bool TryFindValidPlacementScreenPoint(
            BoardScenePresenter presenter,
            GridPlacementController controller,
            Camera camera,
            out Vector2 screenPoint)
        {
            BoardDefinition definition = presenter.Board;
            var board = new GridBoard(definition, presenter.transform.position);
            var validator = new PlacementValidator(board, controller.Occupancy);
            var uiResults = new List<RaycastResult>();

            for (int y = 0; y < definition.Dimensions.Height; y++)
            {
                for (int z = 0; z < definition.Dimensions.Depth; z++)
                {
                    for (int x = 0; x < definition.Dimensions.Width; x++)
                    {
                        var cell = new GridCell(x, z, y);
                        if (!validator.Evaluate(
                                cell,
                                controller.SelectedTower.Footprint).Succeeded)
                        {
                            continue;
                        }

                        Vector3 projected = camera.WorldToScreenPoint(
                            board.Mapper.CellToWorldCenter(cell));
                        var candidate = new Vector2(projected.x, projected.y);
                        if (projected.z <= 0f
                            || !camera.pixelRect.Contains(candidate)
                            || IsOverUi(candidate, uiResults))
                        {
                            continue;
                        }

                        screenPoint = candidate;
                        return true;
                    }
                }
            }

            screenPoint = default;
            return false;
        }

        private static bool IsOverUi(
            Vector2 screenPoint,
            List<RaycastResult> results)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            results.Clear();
            var eventData = new PointerEventData(eventSystem)
            {
                position = screenPoint
            };
            eventSystem.RaycastAll(eventData, results);
            return results.Count > 0;
        }
    }
}
