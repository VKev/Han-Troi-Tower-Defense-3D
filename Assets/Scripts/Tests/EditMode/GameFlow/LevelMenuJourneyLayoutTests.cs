using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow.Tests.EditMode
{
    /// <summary>
    /// Guards the authored journey screen. The screen is built by
    /// <c>Tools/Tower Defense/Rebuild Level Menu Journey Layout</c>, and the failures worth catching
    /// are the ones that look like a broken screen rather than an exception: a panel whose label was
    /// never filled in, a node that does not say which of the three states it is in, and a map that
    /// can only be dragged by grabbing a node.
    /// </summary>
    public sealed class LevelMenuJourneyLayoutTests
    {
        private const string ApplicationUiPrefabPath = "Assets/Resources/Prefabs/ApplicationUI.prefab";
        private const string LevelCatalogPath = "Assets/Config/GameFlow/LevelCatalog.asset";

        /// <summary>Levels one through four are open, so four is the level the player is on.</summary>
        private const int UnlockedThrough = 4;

        private GameObject owner;
        private LevelMenuView menu;
        private List<LevelMenuItemState> states;

        [SetUp]
        public void SetUp()
        {
            LevelCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(LevelCatalogPath);
            Assert.That(catalog, Is.Not.Null, "Level catalog is missing at " + LevelCatalogPath);

            owner = PrefabUtility.LoadPrefabContents(ApplicationUiPrefabPath);
            menu = owner.GetComponentInChildren<LevelMenuView>(true);
            Assert.That(menu, Is.Not.Null, "Application UI prefab has no level menu.");

            states = new List<LevelMenuItemState>(catalog.Levels.Count);
            for (int index = 0; index < catalog.Levels.Count; index++)
            {
                LevelCatalogEntry entry = catalog.Levels[index];
                states.Add(new LevelMenuItemState(
                    entry.LevelNumber,
                    entry.DisplayName,
                    isUnlocked: entry.LevelNumber <= UnlockedThrough,
                    isBusy: false));
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (owner != null)
            {
                PrefabUtility.UnloadPrefabContents(owner);
                owner = null;
            }
        }

        [Test]
        public void JourneyMap_HasADragSurfaceOverTheWholeViewport()
        {
            var scroll = owner.GetComponentInChildren<ScrollRect>(true);
            Assert.That(scroll, Is.Not.Null, "The journey map needs a scroll view.");
            Assert.That(scroll.horizontal, Is.True);
            Assert.That(scroll.viewport, Is.Not.Null);

            Graphic surface = scroll.viewport.GetComponent<Graphic>();
            Assert.That(
                surface,
                Is.Not.Null,
                "The viewport needs a graphic, or a press on the empty map hits nothing and the "
                + "trail can only be dragged by grabbing a node.");
            Assert.That(surface.raycastTarget, Is.True);
            Assert.That(scroll.viewport.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(scroll.viewport.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(scroll.viewport.offsetMin, Is.EqualTo(Vector2.zero));
            Assert.That(scroll.viewport.offsetMax, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void JourneyBackdrop_IsPaintedWithTheAuthoredArt()
        {
            var backdrop = GetPrivateField<GameObject>(menu, "backdrop").GetComponent<Image>();
            Assert.That(backdrop, Is.Not.Null, "The journey map needs a backdrop graphic.");
            // Which sky it is stays the artist's call, so only that there is one is checked here.
            Assert.That(
                backdrop.sprite,
                Is.Not.Null,
                "The backdrop lost its art and fell back to a flat colour.");
            // The exact tint is the artist's call; what matters is that it does not hide the art.
            Assert.That(backdrop.color.a, Is.EqualTo(1f).Within(0.001f), "The sky must be opaque.");
            Assert.That(
                Mathf.Min(backdrop.color.r, backdrop.color.g, backdrop.color.b),
                Is.GreaterThan(0.5f),
                "The sky is tinted dark enough to smother its own art.");

            // Stretched rather than aspect-fitted: fitting would letterbox the very edges this
            // backdrop was moved out of the safe area to reach.
            Assert.That(backdrop.preserveAspect, Is.False);
        }

        [Test]
        public void Show_FillsTheStandingCluster()
        {
            menu.Show(states, _ => { });

            Assert.That(
                ReadText(menu, "progressLabel"),
                Is.EqualTo(UnlockedThrough + "/" + states.Count));
            Assert.That(
                GetPrivateField<Image>(menu, "progressFill").fillAmount,
                Is.EqualTo(UnlockedThrough / (float)states.Count).Within(0.001f));
            Assert.That(ReadText(menu, "subtitleLabel"), Does.StartWith(states.Count.ToString()));
            Assert.That(ReadText(menu, "starLabel"), Does.Contain("/" + (states.Count * 3)));
        }

        [Test]
        public void Show_MarksBeatenCurrentAndLockedNodesApart()
        {
            menu.Show(states, _ => { });

            LevelButtonView beaten = FindNode(UnlockedThrough - 1);
            Assert.That(IsShowing(beaten, "unlockedIndicator"), Is.True, "A beaten node needs its tick.");
            Assert.That(IsShowingLabel(beaten, "starsLabel"), Is.True, "A beaten node needs its stars.");
            Assert.That(IsShowing(beaten, "currentBadge"), Is.False);
            Assert.That(IsShowing(beaten, "lockedIndicator"), Is.False);

            LevelButtonView current = FindNode(UnlockedThrough);
            Assert.That(IsShowing(current, "currentBadge"), Is.True, "The current node needs its badge.");
            Assert.That(IsShowing(current, "ringIndicator"), Is.True, "The current node needs its ring.");
            Assert.That(IsShowing(current, "lockedIndicator"), Is.False);

            LevelButtonView locked = FindNode(UnlockedThrough + 1);
            Assert.That(IsShowing(locked, "lockedIndicator"), Is.True, "A locked node needs its padlock.");
            Assert.That(IsShowingLabel(locked, "requirementLabel"), Is.True);
            Assert.That(IsShowing(locked, "ringIndicator"), Is.False);
            Assert.That(IsShowing(locked, "unlockedIndicator"), Is.False);
        }

        [Test]
        public void Show_NamesTheLevelItSelectsInTheSelectionPanel()
        {
            menu.Show(states, _ => { });

            // The menu opens on the level the player is up to. Only the chapter line numbers
            // it - the title carries the level's authored name, read back off the catalog so
            // renaming a level does not need this test edited too.
            string number = UnlockedThrough.ToString("00");
            LevelMenuItemState selected = states.Find(state => state.LevelNumber == UnlockedThrough);
            // LevelMenuItemState is a struct, so a miss returns default rather than null.
            Assert.That(selected.LevelNumber, Is.EqualTo(UnlockedThrough));
            Assert.That(selected.DisplayName, Is.Not.Empty);
            Assert.That(GetPrivateField<GameObject>(menu, "selectionPanel").activeSelf, Is.True);
            Assert.That(ReadText(menu, "selectionChapter"), Does.Contain(number));
            Assert.That(
                ReadText(menu, "selectionTitle"),
                Is.EqualTo(selected.DisplayName.ToUpperInvariant()));
            Assert.That(ReadText(menu, "selectionDetails"), Is.Not.Empty);
        }

        [Test]
        public void EveryAuthoredLabelSaysSomething()
        {
            menu.Show(states, _ => { });

            var labels = new List<Text>();
            labels.AddRange(menu.GetComponentsInChildren<Text>(true));
            labels.AddRange(
                GetPrivateField<GameObject>(menu, "backdrop").GetComponentsInChildren<Text>(true));
            Assert.That(labels, Is.Not.Empty);
            for (int index = 0; index < labels.Count; index++)
            {
                Assert.That(
                    labels[index].text,
                    Is.Not.Empty,
                    "Blank label at " + BuildPath(labels[index].transform)
                    + ": an authored panel with nothing in it reads as a broken screen.");
                Assert.That(
                    labels[index].font,
                    Is.Not.Null,
                    "Fontless label at " + BuildPath(labels[index].transform));
            }
        }

        private LevelButtonView FindNode(int levelNumber)
        {
            var nodes = GetPrivateField<LevelButtonView[]>(menu, "levelButtons");
            for (int index = 0; index < nodes.Length; index++)
            {
                if (GetPrivateField<int>(nodes[index], "levelNumber") == levelNumber)
                {
                    return nodes[index];
                }
            }

            Assert.Fail("The journey has no node for level " + levelNumber + ".");
            return null;
        }

        private static bool IsShowing(LevelButtonView node, string fieldName)
        {
            GameObject part = GetPrivateField<GameObject>(node, fieldName);
            Assert.That(part, Is.Not.Null, "Node is missing its " + fieldName + ".");
            return part.activeSelf;
        }

        private static bool IsShowingLabel(LevelButtonView node, string fieldName)
        {
            Text part = GetPrivateField<Text>(node, fieldName);
            Assert.That(part, Is.Not.Null, "Node is missing its " + fieldName + ".");
            return part.gameObject.activeSelf;
        }

        private static string ReadText(Object target, string fieldName)
        {
            Text label = GetPrivateField<Text>(target, fieldName);
            Assert.That(label, Is.Not.Null, "Missing label '" + fieldName + "'.");
            return label.text;
        }

        private static string BuildPath(Transform node)
        {
            string path = node.name;
            for (Transform parent = node.parent; parent != null; parent = parent.parent)
            {
                path = parent.name + "/" + path;
            }

            return path;
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            return (T)field.GetValue(target);
        }
    }
}
