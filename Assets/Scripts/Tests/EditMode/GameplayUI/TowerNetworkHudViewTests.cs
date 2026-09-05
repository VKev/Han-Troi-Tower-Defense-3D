using NUnit.Framework;
using TowerDefense3D.Towers;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow.Tests.EditMode
{
    public sealed class TowerNetworkHudViewTests
    {
        private const string GameplayUiPrefabPath = "Assets/Resources/Prefabs/GameplayUI.prefab";
        private const string TowerCatalogPath = "Assets/Config/Towers/Catalogs/TowerCatalog.asset";

        private GameObject owner;
        private GameObject eventSystemOwner;
        private EventSystem eventSystem;
        private GraphicRaycaster graphicRaycaster;
        private TowerCatalog catalog;
        private TowerNetworkHudView view;

        [SetUp]
        public void SetUp()
        {
            catalog = AssetDatabase.LoadAssetAtPath<TowerCatalog>(TowerCatalogPath);
            Assert.That(catalog, Is.Not.Null, $"Tower Catalog is missing at '{TowerCatalogPath}'.");

            owner = PrefabUtility.LoadPrefabContents(GameplayUiPrefabPath);
            Assert.That(owner, Is.Not.Null, $"Gameplay UI prefab is missing at '{GameplayUiPrefabPath}'.");
            view = owner.GetComponentInChildren<TowerNetworkHudView>(true);
            Assert.That(view, Is.Not.Null, "Gameplay UI prefab must author a TowerNetworkHudView.");
            Assert.That(owner.GetComponent<GameplayUIView>(), Is.Not.Null);
            graphicRaycaster = owner.GetComponent<GraphicRaycaster>();
            Assert.That(graphicRaycaster, Is.Not.Null);

            eventSystemOwner = new GameObject("Tower Network HUD Event System", typeof(EventSystem));
            eventSystem = eventSystemOwner.GetComponent<EventSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            if (view != null)
            {
                view.Shutdown();
            }

            if (owner != null)
            {
                PrefabUtility.UnloadPrefabContents(owner);
            }

            if (eventSystemOwner != null)
            {
                Object.DestroyImmediate(eventSystemOwner);
            }
        }

        [Test]
        public void Prefab_AuthorsOneCatalogMappedPanelInsideTheExistingSafeArea()
        {
            view.Initialize();

            Transform safeArea = owner.transform.Find("Safe Area");
            Assert.That(safeArea, Is.Not.Null);
            Transform panel = safeArea.Find("Tower Network HUD");
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.parent, Is.EqualTo(safeArea));
            Assert.That(panel.GetComponentsInChildren<Button>(true).Length, Is.EqualTo(catalog.Definitions.Count + 4));
            Assert.That(panel.Find("Tower Buttons").childCount, Is.EqualTo(catalog.Definitions.Count));
            Assert.That(
                panel.GetComponentsInChildren<TowerPlacementDragButtonView>(true).Length,
                Is.EqualTo(catalog.Definitions.Count));
            Assert.That(safeArea.Find("Select Tower"), Is.Null);
            Assert.That(panel.Find("Tower Actions/Unlink").GetComponent<Button>(), Is.Not.Null);
            Assert.That(panel.Find("Tower Actions/Sell").GetComponent<Button>(), Is.Not.Null);
            Assert.That(panel.Find("Tower Actions/Upgrade").GetComponent<Button>(), Is.Not.Null);
            Assert.That(panel.Find("Start Wave").GetComponent<Button>(), Is.Not.Null);

            view.Initialize();

            Assert.That(CountDirectChildrenNamed(safeArea, "Tower Network HUD"), Is.EqualTo(1));
        }

        [Test]
        public void RenderAndButtons_ExposeNetworkActionsAndSimulationGate()
        {
            bool unlinkRequested = false;
            bool sellRequested = false;
            bool upgradeRequested = false;
            view.UnlinkRequested += () => unlinkRequested = true;
            view.SellRequested += () => sellRequested = true;
            view.UpgradeRequested += () => upgradeRequested = true;
            view.Initialize();

            Transform panel = view.transform;
            Button firstTowerButton = panel.Find("Tower Buttons").GetChild(0).GetComponent<Button>();
            Button unlinkButton = panel.Find("Tower Actions/Unlink").GetComponent<Button>();
            Button sellButton = panel.Find("Tower Actions/Sell").GetComponent<Button>();
            Button upgradeButton = panel.Find("Tower Actions/Upgrade").GetComponent<Button>();
            var state = new TowerNetworkHudState(
                "Selected: Generator",
                "Ready.",
                towerSelectionEnabled: false,
                unlinkEnabled: true,
                sellEnabled: true,
                towerActionsVisible: true,
                towerActionsScreenPosition: new Vector2(Screen.width * 0.5f, Screen.height * 0.5f),
                upgradeEnabled: true,
                upgradeCostText: "110",
                sellRefundText: "154");

            view.Render(state);

            Assert.That(firstTowerButton.interactable, Is.False);
            Assert.That(unlinkButton.interactable, Is.True);
            Assert.That(sellButton.interactable, Is.True);
            Assert.That(upgradeButton.interactable, Is.True);

            // The prices ride in the dark strip the button art already has.
            Assert.That(
                upgradeButton.transform.Find("Cost").GetComponent<Text>().text,
                Is.EqualTo("110"));
            Assert.That(
                sellButton.transform.Find("Cost").GetComponent<Text>().text,
                Is.EqualTo("154"));

            firstTowerButton.onClick.Invoke();
            unlinkButton.onClick.Invoke();
            sellButton.onClick.Invoke();
            upgradeButton.onClick.Invoke();

            Assert.That(unlinkRequested, Is.True);
            Assert.That(sellRequested, Is.True);
            Assert.That(upgradeRequested, Is.True);
        }

        [Test]
        public void TowerButton_DragPublishesDefinitionPointerPositionAndUiState()
        {
            TowerCombatDefinition beganDefinition = null;
            TowerPlacementPointerEvent beganEvent = default;
            TowerPlacementPointerEvent movedEvent = default;
            TowerPlacementPointerEvent endedEvent = default;
            int beginCount = 0;
            int canceledPointerId = 0;
            view.TowerDragBegan += (definition, pointerEvent) =>
            {
                beganDefinition = definition;
                beganEvent = pointerEvent;
                beginCount++;
            };
            view.TowerDragMoved += pointerEvent => movedEvent = pointerEvent;
            view.TowerDragEnded += pointerEvent => endedEvent = pointerEvent;
            view.TowerDragCanceled += pointerId => canceledPointerId = pointerId;
            view.Initialize();

            Transform panel = view.transform;
            Button firstButton = panel.Find("Tower Buttons").GetChild(0).GetComponent<Button>();
            var startPosition = new Vector2(40f, 60f);
            var boardPosition = new Vector2(500f, 320f);

            firstButton.onClick.Invoke();
            Assert.That(beginCount, Is.Zero, "A short click must not arm tower placement.");

            PointerEventData eventData = CreatePointerEvent(-1, startPosition, firstButton.gameObject);
            ExecuteEvents.Execute(firstButton.gameObject, eventData, ExecuteEvents.beginDragHandler);

            eventData.position = boardPosition;
            eventData.pointerCurrentRaycast = default;
            ExecuteEvents.Execute(firstButton.gameObject, eventData, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(firstButton.gameObject, eventData, ExecuteEvents.endDragHandler);

            Assert.That(beginCount, Is.EqualTo(1));
            Assert.That(beganDefinition, Is.EqualTo(catalog.Definitions[0]));
            Assert.That(beganEvent.PointerId, Is.EqualTo(-1));
            Assert.That(beganEvent.ScreenPosition, Is.EqualTo(startPosition));
            Assert.That(beganEvent.IsOverUi, Is.True);
            Assert.That(movedEvent.ScreenPosition, Is.EqualTo(boardPosition));
            Assert.That(movedEvent.IsOverUi, Is.False);
            Assert.That(endedEvent.ScreenPosition, Is.EqualTo(boardPosition));
            Assert.That(endedEvent.IsOverUi, Is.False);

            eventData = CreatePointerEvent(17, startPosition, firstButton.gameObject);
            ExecuteEvents.Execute(firstButton.gameObject, eventData, ExecuteEvents.beginDragHandler);
            firstButton.GetComponent<TowerPlacementDragButtonView>().SetInteractable(false);

            Assert.That(canceledPointerId, Is.EqualTo(17));
        }

        private PointerEventData CreatePointerEvent(int pointerId, Vector2 position, GameObject uiTarget)
        {
            return new PointerEventData(eventSystem)
            {
                pointerId = pointerId,
                position = position,
                pointerCurrentRaycast = new RaycastResult
                {
                    gameObject = uiTarget,
                    module = graphicRaycaster
                }
            };
        }

        private static int CountDirectChildrenNamed(Transform parent, string childName)
        {
            int count = 0;
            for (int index = 0; index < parent.childCount; index++)
            {
                if (parent.GetChild(index).name == childName)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
