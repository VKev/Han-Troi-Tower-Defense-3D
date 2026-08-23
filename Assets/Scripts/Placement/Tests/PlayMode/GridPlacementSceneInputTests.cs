using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TowerDefense3D.Towers;
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

            GridPlacementPresenter controller =
                Object.FindFirstObjectByType<GridPlacementPresenter>();
            GameObject placedRoot = GameObject.Find("Grid Placement/Placed Towers");
            GameObject boardOrigin = GameObject.Find("Grid Placement/Board Origin");
            Camera camera = Camera.main;
            BoardView presenter =
                boardOrigin != null
                    ? boardOrigin.GetComponent<BoardView>()
                    : null;

            Assert.That(controller, Is.Not.Null);
            controller.Initialize();
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

        [UnityTest]
        public IEnumerator UiDrag_MousePointer_PlacesOnceThenClearsSelectionAndPreview()
        {
            yield return SceneManager.LoadSceneAsync(
                "Assets/Scenes/Levels/Level_001.unity",
                LoadSceneMode.Single);
            yield return null;
            yield return null;

            GridPlacementPresenter controller =
                Object.FindFirstObjectByType<GridPlacementPresenter>();
            GameObject placedRoot = GameObject.Find("Grid Placement/Placed Towers");
            GameObject boardOrigin = GameObject.Find("Grid Placement/Board Origin");
            Camera camera = Camera.main;
            BoardView presenter =
                boardOrigin != null
                    ? boardOrigin.GetComponent<BoardView>()
                    : null;

            Assert.That(controller, Is.Not.Null);
            controller.Initialize();
            Assert.That(controller.SelectedTower, Is.Not.Null);
            Assert.That(placedRoot, Is.Not.Null);
            Assert.That(presenter, Is.Not.Null);
            Assert.That(camera, Is.Not.Null);

            TowerDefinition placementDefinition = controller.SelectedTower;
            var combatDefinition = ScriptableObject.CreateInstance<GeneratorTowerDefinition>();
            SetPrivateField(combatDefinition.Core, "placementDefinition", placementDefinition);

            try
            {
                Assert.That(
                    TryFindValidPlacementScreenPoint(
                        presenter,
                        controller,
                        camera,
                        out Vector2 screenPoint),
                    Is.True);

                Assert.That(
                    controller.BeginPlacementDrag(combatDefinition, -1),
                    Is.True);
                Assert.That(controller.HasCandidate, Is.False);

                controller.UpdatePlacementDrag(-1, screenPoint, pointerOverUi: false);

                Assert.That(controller.HasCandidate, Is.True);
                Assert.That(controller.CandidateIsValid, Is.True);
                Assert.That(
                    controller.EndPlacementDrag(-1, screenPoint, pointerOverUi: false),
                    Is.True);

                Assert.That(placedRoot.transform.childCount, Is.EqualTo(1));
                Assert.That(controller.SelectedTower, Is.Null);
                Assert.That(controller.SelectedCombatDefinition, Is.Null);
                Assert.That(controller.HasCandidate, Is.False);
                Assert.That(
                    Quaternion.Angle(
                        placedRoot.transform.GetChild(0).rotation,
                        placementDefinition.Prefab.transform.rotation),
                    Is.LessThan(0.01f));

                Assert.That(
                    controller.BeginPlacementDrag(combatDefinition, -1),
                    Is.True);
                controller.UpdatePlacementDrag(-1, screenPoint, pointerOverUi: true);

                Assert.That(controller.HasCandidate, Is.False);
                Assert.That(
                    controller.EndPlacementDrag(-1, screenPoint, pointerOverUi: true),
                    Is.False);
                Assert.That(placedRoot.transform.childCount, Is.EqualTo(1));
                Assert.That(controller.SelectedTower, Is.Null);
                Assert.That(controller.HasCandidate, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(combatDefinition);
            }
        }

        private static bool TryFindValidPlacementScreenPoint(
            BoardView presenter,
            GridPlacementPresenter controller,
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

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}'.");
            field.SetValue(target, value);
        }
    }
}
