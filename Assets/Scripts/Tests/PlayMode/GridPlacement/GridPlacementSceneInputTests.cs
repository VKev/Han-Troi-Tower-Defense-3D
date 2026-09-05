using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TowerDefense3D.GameplayInput;
using TowerDefense3D.Towers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TowerDefense3D.GridPlacement.Tests.PlayMode
{
    public sealed class GridPlacementSceneInputTests
    {
        private readonly List<InputDevice> suspendedMice = new List<InputDevice>();

        private Mouse testMouse;

        [TearDown]
        public void TearDown()
        {
            if (testMouse != null && testMouse.added)
            {
                InputSystem.RemoveDevice(testMouse);
            }

            testMouse = null;

            for (int index = 0; index < suspendedMice.Count; index++)
            {
                InputSystem.EnableDevice(suspendedMice[index]);
            }

            suspendedMice.Clear();
        }

        /// <summary>
        /// Adds a mouse for the test to drive, and suspends the machine's own for the duration.
        ///
        /// The input this test reads comes from <c>Mouse.current</c>, which follows whichever mouse
        /// last sent input. The editor runs on a machine with a real mouse attached, so any stray
        /// movement of it while the test ran handed <c>current</c> back to the hardware and left the
        /// queued state on this one invisible. That is what made this test fail every so often -
        /// more often in a full run, simply because a full run gives the hand at the desk longer to
        /// interfere.
        /// </summary>
        private Mouse AddIsolatedTestMouse()
        {
            for (int index = 0; index < InputSystem.devices.Count; index++)
            {
                InputDevice device = InputSystem.devices[index];
                if (device is Mouse && device.enabled)
                {
                    suspendedMice.Add(device);
                }
            }

            for (int index = 0; index < suspendedMice.Count; index++)
            {
                InputSystem.DisableDevice(suspendedMice[index]);
            }

            return InputSystem.AddDevice<Mouse>();
        }

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
            GridPlacementView placementView =
                Object.FindFirstObjectByType<GridPlacementView>();
            TowerInstanceFactory instanceFactory =
                Object.FindFirstObjectByType<TowerInstanceFactory>();
            GameplayInputSource inputSource =
                Object.FindFirstObjectByType<GameplayInputSource>();

            Assert.That(controller, Is.Not.Null);
            Assert.That(placedRoot, Is.Not.Null);
            Assert.That(boardOrigin, Is.Not.Null);
            Assert.That(presenter, Is.Not.Null);
            Assert.That(placementView, Is.Not.Null);
            Assert.That(instanceFactory, Is.Not.Null);
            Assert.That(inputSource, Is.Not.Null);
            Assert.That(camera, Is.Not.Null);
            Assert.That(placedRoot.transform.childCount, Is.Zero);

            var boardSystem = new BoardSystem(presenter);
            var inputSystem = new GameplayInputSystem(inputSource);
            var placementSystem = new GridPlacementSystem(
                boardSystem,
                inputSystem,
                placementView,
                instanceFactory);
            boardSystem.Start();
            inputSystem.Start();
            controller.Bind(placementSystem, placementView);

            Assert.That(controller.Occupancy, Is.Not.Null);
            Assert.That(
                controller.SelectedTower,
                Is.Null,
                "Entering a level must not arm a tower on its own.");

            controller.SelectTower(CreateTestPlacementDefinition());
            Assert.That(controller.SelectedTower, Is.Not.Null);

            testMouse = AddIsolatedTestMouse();
            Mouse mouse = testMouse;
            Assert.That(
                TryFindValidPlacementScreenPoint(
                    presenter,
                    placementSystem,
                    camera,
                    out Vector2 screenPoint),
                Is.True,
                "The authored level needs one visible, non-UI placement point "
                + "for the selected tower.");
            Assert.That(
                placementView.TryGetWorldPoint(screenPoint, out _),
                Is.True,
                "The placement view must project the selected screen point onto the authored board.");

            yield return null;
            QueueMouseState(mouse, screenPoint, isPressed: true);
            inputSystem.Tick();
            Assert.That(inputSystem.Current.HasPointerInput, Is.True);
            Assert.That(inputSystem.Current.WasPressed, Is.True);
            Assert.That(inputSystem.Current.IsPointerOverUi, Is.False);
            placementSystem.Tick();

            Assert.That(controller.HasCandidate, Is.True);
            Assert.That(controller.CandidateIsValid, Is.True);

            yield return null;
            QueueMouseState(mouse, screenPoint, isPressed: false);
            inputSystem.Tick();
            placementSystem.Tick();

            Assert.That(placedRoot.transform.childCount, Is.EqualTo(1));
            Assert.That(controller.HasCandidate, Is.True);
            Assert.That(controller.CandidateIsValid, Is.False);

            yield return null;
            QueueMouseState(mouse, screenPoint, isPressed: true);
            inputSystem.Tick();
            placementSystem.Tick();
            yield return null;
            QueueMouseState(mouse, screenPoint, isPressed: false);
            inputSystem.Tick();
            placementSystem.Tick();

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
            GridPlacementView placementView =
                Object.FindFirstObjectByType<GridPlacementView>();
            TowerInstanceFactory instanceFactory =
                Object.FindFirstObjectByType<TowerInstanceFactory>();
            GameplayInputSource inputSource =
                Object.FindFirstObjectByType<GameplayInputSource>();

            Assert.That(controller, Is.Not.Null);
            Assert.That(placedRoot, Is.Not.Null);
            Assert.That(presenter, Is.Not.Null);
            Assert.That(placementView, Is.Not.Null);
            Assert.That(instanceFactory, Is.Not.Null);
            Assert.That(inputSource, Is.Not.Null);
            Assert.That(camera, Is.Not.Null);

            var boardSystem = new BoardSystem(presenter);
            var inputSystem = new GameplayInputSystem(inputSource);
            var placementSystem = new GridPlacementSystem(
                boardSystem,
                inputSystem,
                placementView,
                instanceFactory);
            boardSystem.Start();
            inputSystem.Start();
            controller.Bind(placementSystem, placementView);

            Assert.That(
                controller.SelectedTower,
                Is.Null,
                "Entering a level must not arm a tower on its own.");

            controller.SelectTower(CreateTestPlacementDefinition());
            Assert.That(controller.SelectedTower, Is.Not.Null);

            TowerDefinition placementDefinition = controller.SelectedTower;
            var combatDefinition = ScriptableObject.CreateInstance<GeneratorTowerDefinition>();
            SetPrivateField(combatDefinition.Core, "placementDefinition", placementDefinition);

            try
            {
                Assert.That(
                    TryFindValidPlacementScreenPoint(
                        presenter,
                        placementSystem,
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

        /// <summary>
        /// Stands in for what a build button hands over, now that binding arms nothing.
        /// </summary>
        /// <remarks>
        /// A prefab is required only because the candidate is refused without one; the placement
        /// factory in these tests is a stub, so nothing is ever spawned from it.
        /// </remarks>
        private static TowerDefinition CreateTestPlacementDefinition()
        {
            var definition = ScriptableObject.CreateInstance<TowerDefinition>();
            SetPrivateField(definition, "prefab", new GameObject("Test Tower Prefab"));
            SetPrivateField(definition, "footprint", new TowerFootprint(1, 1, 1));
            return definition;
        }

        private static bool TryFindValidPlacementScreenPoint(
            BoardView presenter,
            GridPlacementSystem placementSystem,
            Camera camera,
            out Vector2 screenPoint)
        {
            BoardDefinition definition = presenter.Board;
            var board = new GridBoard(definition, presenter.transform.position);
            var validator = new PlacementValidator(board, placementSystem.Occupancy);
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
                                placementSystem.SelectedTower.Footprint).Succeeded)
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

        /// <summary>
        /// Queues one mouse state and applies it there and then.
        ///
        /// A queued event only reaches the device on the next input update, and the press it
        /// carries is readable as <c>wasPressedThisFrame</c> for exactly that one update. Leaving
        /// that to the player loop made this a race the test lost every so often: the caller reads
        /// the device on the next frame, and whether the input update had landed by then - or had
        /// already been followed by another that cleared the edge - was not something the test
        /// controlled. Applying it here means the caller reads the press in the same frame it was
        /// applied, with nothing in between.
        /// </summary>
        private static void QueueMouseState(Mouse mouse, Vector2 position, bool isPressed)
        {
            MouseState state = new MouseState
            {
                position = position
            }.WithButton(MouseButton.Left, isPressed);
            InputSystem.QueueStateEvent(mouse, state);
            InputSystem.Update();
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
