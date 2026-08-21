using NUnit.Framework;
using TowerDefense3D.Towers;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow.Tests.EditMode
{
    public sealed class TowerNetworkHudViewTests
    {
        private const string TowerCatalogPath = "Assets/Config/Towers/Catalogs/TowerCatalog.asset";

        private GameObject owner;
        private RectTransform safeArea;
        private TowerCatalog catalog;
        private TowerNetworkHudView view;

        [SetUp]
        public void SetUp()
        {
            catalog = AssetDatabase.LoadAssetAtPath<TowerCatalog>(TowerCatalogPath);
            Assert.That(catalog, Is.Not.Null, $"Tower Catalog is missing at '{TowerCatalogPath}'.");

            owner = new GameObject("Tower Network HUD Test", typeof(RectTransform), typeof(Canvas));
            view = owner.AddComponent<TowerNetworkHudView>();

            var safeAreaObject = new GameObject("Safe Area", typeof(RectTransform));
            safeArea = safeAreaObject.GetComponent<RectTransform>();
            safeArea.SetParent(owner.transform, false);
            safeArea.anchorMin = Vector2.zero;
            safeArea.anchorMax = Vector2.one;
            safeArea.sizeDelta = Vector2.zero;
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
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void Initialize_BuildsOneCatalogDrivenPanelInsideTheExistingSafeArea()
        {
            view.Initialize(safeArea, catalog);

            Transform panel = safeArea.Find("Tower Network HUD");
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.parent, Is.EqualTo(safeArea));
            Assert.That(panel.GetComponentsInChildren<Button>(true).Length, Is.EqualTo(catalog.Definitions.Count + 2));
            Assert.That(panel.Find("Tower Buttons").childCount, Is.EqualTo(catalog.Definitions.Count));
            Assert.That(panel.Find("Unlink").GetComponent<Button>(), Is.Not.Null);
            Assert.That(panel.Find("Start Wave").GetComponent<Button>(), Is.Not.Null);

            view.Initialize(safeArea, catalog);

            Assert.That(CountDirectChildrenNamed(safeArea, "Tower Network HUD"), Is.EqualTo(1));
        }

        [Test]
        public void RenderAndButtons_ExposeSelectionActionsAndSimulationGate()
        {
            TowerCombatDefinition requestedTower = null;
            bool unlinkRequested = false;
            bool startWaveRequested = false;
            view.TowerRequested += definition => requestedTower = definition;
            view.UnlinkRequested += () => unlinkRequested = true;
            view.StartWaveRequested += () => startWaveRequested = true;
            view.Initialize(safeArea, catalog);

            Transform panel = safeArea.Find("Tower Network HUD");
            Button firstTowerButton = panel.Find("Tower Buttons").GetChild(0).GetComponent<Button>();
            Button unlinkButton = panel.Find("Unlink").GetComponent<Button>();
            Button startWaveButton = panel.Find("Start Wave").GetComponent<Button>();
            var state = new TowerNetworkHudState(
                "Selected: Generator",
                "Valid chains: 1",
                "Queue: 0 / 3",
                "Ready.",
                towerSelectionEnabled: false,
                unlinkEnabled: true,
                startWaveEnabled: true,
                startWaveText: "RUNNING");

            view.Render(state);

            Assert.That(firstTowerButton.interactable, Is.False);
            Assert.That(unlinkButton.interactable, Is.True);
            Assert.That(startWaveButton.interactable, Is.True);
            Assert.That(startWaveButton.GetComponentInChildren<Text>().text, Is.EqualTo("RUNNING"));
            Assert.That(panel.Find("Selected Status").GetComponent<Text>().text, Is.EqualTo("Selected: Generator"));
            Assert.That(panel.Find("Chain Status").GetComponent<Text>().text, Is.EqualTo("Valid chains: 1"));

            firstTowerButton.onClick.Invoke();
            unlinkButton.onClick.Invoke();
            startWaveButton.onClick.Invoke();

            Assert.That(requestedTower, Is.EqualTo(catalog.Definitions[0]));
            Assert.That(unlinkRequested, Is.True);
            Assert.That(startWaveRequested, Is.True);
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
