using NUnit.Framework;
using TowerDefense3D.GridPlacement;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.GameFlow.Tests.EditMode
{
    /// <summary>
    /// A screen-filling backdrop parented under the safe area stops at the safe area, leaving a
    /// border of whatever is behind it - the wooden desktop on a notched phone, live gameplay
    /// behind a victory panel. These check that the backdrops sit outside the safe area and cover
    /// the whole display, and that what they say still sits inside it.
    /// </summary>
    public sealed class FullBleedOverlayTests
    {
        private const string ApplicationUiPrefabPath = "Assets/Resources/Prefabs/ApplicationUI.prefab";
        private const string GameplayUiPrefabPath = "Assets/Resources/Prefabs/GameplayUI.prefab";

        [TestCase("Journey Map")]
        [TestCase("Loading")]
        [TestCase("Blocking Error")]
        [TestCase("Input Blocker")]
        public void ApplicationUiOverlay_CoversTheWholeDisplay(string name)
        {
            AssertFullBleed(ApplicationUiPrefabPath, name);
        }

        [Test]
        public void OutcomePanel_CoversTheWholeDisplay()
        {
            AssertFullBleed(GameplayUiPrefabPath, "Outcome HUD");
        }

        [Test]
        public void OutcomePanel_KeepsItsCardInsideTheSafeArea()
        {
            GameObject owner = PrefabUtility.LoadPrefabContents(GameplayUiPrefabPath);
            try
            {
                RectTransform card = FindRect(owner, "Outcome HUD/Outcome Root/Outcome Card");

                // The card is what the player reads and taps, so it has to stay clear of a cutout.
                // Half a reference display of margin on either side is plenty for any notch.
                Assert.That(card.sizeDelta.x, Is.LessThan(1920f * 0.75f));
                Assert.That(card.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)));
                Assert.That(card.anchorMax, Is.EqualTo(new Vector2(0.5f, 0.5f)));
                Assert.That(card.anchoredPosition, Is.EqualTo(Vector2.zero));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(owner);
            }
        }

        /// <summary>
        /// The save warning is a banner pinned to the top edge, which is where a notch is, so it is
        /// the one overlay that has to stay inset.
        /// </summary>
        [Test]
        public void SaveWarningBanner_StaysInsideTheSafeArea()
        {
            GameObject owner = PrefabUtility.LoadPrefabContents(ApplicationUiPrefabPath);
            try
            {
                var safeArea = owner.GetComponentInChildren<SafeAreaView>(true);
                Assert.That(safeArea, Is.Not.Null);
                Assert.That(safeArea.transform.Find("Save Warning"), Is.Not.Null);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(owner);
            }
        }

        private static void AssertFullBleed(string prefabPath, string name)
        {
            GameObject owner = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var safeArea = owner.GetComponentInChildren<SafeAreaView>(true);
                Assert.That(safeArea, Is.Not.Null, prefabPath + " has no safe area.");

                RectTransform overlay = FindRect(owner, name);
                Assert.That(
                    overlay.IsChildOf(safeArea.transform),
                    Is.False,
                    name + " is inside the safe area, so its backdrop stops short of the screen "
                    + "edges and leaves a border of whatever is behind it.");
                Assert.That(overlay.anchorMin, Is.EqualTo(Vector2.zero), name + " anchorMin");
                Assert.That(overlay.anchorMax, Is.EqualTo(Vector2.one), name + " anchorMax");
                Assert.That(overlay.offsetMin, Is.EqualTo(Vector2.zero), name + " offsetMin");
                Assert.That(overlay.offsetMax, Is.EqualTo(Vector2.zero), name + " offsetMax");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(owner);
            }
        }

        private static RectTransform FindRect(GameObject owner, string path)
        {
            Transform found = owner.transform.Find(path);
            Assert.That(found, Is.Not.Null, "Missing " + path + " under " + owner.name + ".");
            return (RectTransform)found;
        }
    }
}
