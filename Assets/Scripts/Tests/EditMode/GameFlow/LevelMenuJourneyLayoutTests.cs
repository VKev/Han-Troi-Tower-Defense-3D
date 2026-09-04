using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
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
                // Everything below the level the player is up to counts as beaten, that level
                // itself as open-but-unbeaten, and the rest as shut - so one fixture exercises
                // all three node states.
                states.Add(new LevelMenuItemState(
                    entry.LevelNumber,
                    entry.DisplayName,
                    isUnlocked: entry.LevelNumber <= UnlockedThrough,
                    isCleared: entry.LevelNumber < UnlockedThrough,
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
        }

        /// <summary>
        /// A node has to say which of its five states it is in, and say only one of them: the
        /// states are five sibling objects, so the failure worth catching is two showing at once
        /// or none showing at all.
        /// </summary>
        [Test]
        public void Show_DrawsBeatenOpenAndShutNodesWithDifferentArt()
        {
            menu.Show(states, _ => { });

            // Show opens on the level the player is up to, so that node already wears its ring.
            // The plain open look therefore has to be read after putting the node back.
            LevelButtonView openNode = FindNode(UnlockedThrough);
            Assert.That(ActiveState(FindNode(UnlockedThrough - 1)), Is.EqualTo("clearedNode"));
            Assert.That(ActiveState(openNode), Is.EqualTo("unlockedSelectedNode"));
            Assert.That(ActiveState(FindNode(UnlockedThrough + 1)), Is.EqualTo("lockedNode"));

            openNode.SetSelected(false);
            Assert.That(ActiveState(openNode), Is.EqualTo("unlockedNode"));

            // A shut node must not be clickable, or the menu would select a level the player
            // has not reached.
            Assert.That(
                GetPrivateField<Button>(FindNode(UnlockedThrough + 1), "button").interactable,
                Is.False,
                "A shut node must not be selectable.");

            // The beaten state has a ring of its own, so a level the player goes back to does
            // not fall back to the plain green body.
            LevelButtonView beatenNode = FindNode(UnlockedThrough - 1);
            beatenNode.SetSelected(true);
            Assert.That(ActiveState(beatenNode), Is.EqualTo("clearedSelectedNode"));

            // Selecting a shut node is not a thing the menu can do, but if it ever did the node
            // must stay grey rather than sprout a ring it cannot honour.
            LevelButtonView shutNode = FindNode(UnlockedThrough + 1);
            shutNode.SetSelected(true);
            Assert.That(ActiveState(shutNode), Is.EqualTo("lockedNode"));
        }

        /// <summary>
        /// The name of the one state field whose object is showing, failing if that is not
        /// exactly one of them.
        /// </summary>
        private static string ActiveState(LevelButtonView view)
        {
            Assert.That(view, Is.Not.Null, "Missing node.");
            string showing = null;
            foreach (string field in new[]
                     {
                         "lockedNode",
                         "unlockedNode",
                         "unlockedSelectedNode",
                         "clearedNode",
                         "clearedSelectedNode"
                     })
            {
                GameObject state = GetPrivateField<GameObject>(view, field);
                Assert.That(state, Is.Not.Null, view.name + " has no " + field + " artwork.");
                if (!state.activeSelf)
                {
                    continue;
                }

                Assert.That(
                    showing,
                    Is.Null,
                    view.name + " shows " + showing + " and " + field + " at the same time.");
                showing = field;
            }

            Assert.That(showing, Is.Not.Null, view.name + " shows no state at all.");
            return showing;
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
            Assert.That(ReadText(menu, "selectionTitle"), Is.EqualTo(selected.DisplayName));
            // "Selected Details" is authored content the view never writes, so there is no
            // field to read here; EveryAuthoredLabelSaysSomething still guards it from going
            // blank.
        }

        [Test]
        public void EveryAuthoredLabelSaysSomething()
        {
            menu.Show(states, _ => { });

            GameObject backdrop = GetPrivateField<GameObject>(menu, "backdrop");

            // Swept separately because the two stacks type their font differently, and a screen
            // that is half TMP would otherwise leave half its labels unchecked.
            var uguiLabels = new List<Text>();
            uguiLabels.AddRange(menu.GetComponentsInChildren<Text>(true));
            uguiLabels.AddRange(backdrop.GetComponentsInChildren<Text>(true));

            var tmpLabels = new List<TMP_Text>();
            tmpLabels.AddRange(menu.GetComponentsInChildren<TMP_Text>(true));
            tmpLabels.AddRange(backdrop.GetComponentsInChildren<TMP_Text>(true));

            Assert.That(uguiLabels.Count + tmpLabels.Count, Is.GreaterThan(0));
            Assert.That(tmpLabels, Is.Not.Empty, "The selection panel is authored on TextMeshPro.");

            for (int index = 0; index < uguiLabels.Count; index++)
            {
                Assert.That(
                    uguiLabels[index].text,
                    Is.Not.Empty,
                    "Blank label at " + BuildPath(uguiLabels[index].transform)
                    + ": an authored panel with nothing in it reads as a broken screen.");
                Assert.That(
                    uguiLabels[index].font,
                    Is.Not.Null,
                    "Fontless label at " + BuildPath(uguiLabels[index].transform));
            }

            for (int index = 0; index < tmpLabels.Count; index++)
            {
                Assert.That(
                    tmpLabels[index].text,
                    Is.Not.Empty,
                    "Blank label at " + BuildPath(tmpLabels[index].transform)
                    + ": an authored panel with nothing in it reads as a broken screen.");

                // An unset TMP font falls back to LiberationSans, whose atlas carries no
                // Vietnamese glyphs - every level name would render as boxes.
                Assert.That(
                    tmpLabels[index].font,
                    Is.Not.Null,
                    "Fontless TMP label at " + BuildPath(tmpLabels[index].transform));
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

        /// <summary>
        /// Reads a label field whichever text component it holds. The selection panel moved to
        /// TextMeshPro while the rest of the screen is still uGUI, so this screen is mixed and
        /// the test should not care which half a field belongs to.
        /// </summary>
        private static string ReadText(Object target, string fieldName)
        {
            object label = GetPrivateField<object>(target, fieldName);
            Assert.That(label, Is.Not.Null, "Missing label '" + fieldName + "'.");
            switch (label)
            {
                case TMP_Text tmp:
                    return tmp.text;
                case Text legacy:
                    return legacy.text;
                default:
                    Assert.Fail(
                        "Field '" + fieldName + "' holds a " + label.GetType().Name
                        + ", which is not a text component.");
                    return null;
            }
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
