using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.GameFlow.Tests.EditMode
{
    /// <summary>
    /// Leaving the journey screen has to take the whole screen with it, and the screen is authored
    /// in two pieces. The chrome sits inside the safe area; the backdrop and the trail behind it
    /// hang off the canvas instead, so they run edge to edge under the notch rather than leaving a
    /// border. Hiding only the piece the view happens to hold a reference to leaves the other one
    /// on top of the level the player just entered.
    ///
    /// Checked against the authored prefab rather than a rig, because the failure this guards
    /// against is not in the hiding - it is a reference the prefab never filled in, which no test
    /// standing up its own objects would ever see.
    /// </summary>
    public sealed class LevelMenuVisibilityTests
    {
        private const string ApplicationUiPrefabPath = "Assets/Resources/Prefabs/ApplicationUI.prefab";

        private GameObject instance;

        [SetUp]
        public void SetUp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ApplicationUiPrefabPath);
            Assert.That(prefab, Is.Not.Null, "The application UI prefab must exist to be checked.");
            instance = Object.Instantiate(prefab);
        }

        [TearDown]
        public void TearDown()
        {
            if (instance != null)
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void HideLevelMenu_TakesTheFullBleedJourneyMapWithTheChrome()
        {
            instance.GetComponent<ApplicationUIView>().HideLevelMenu();

            Assert.That(
                Part("Safe Area/Level Menu").activeSelf,
                Is.False,
                "The journey chrome is still up after the menu was hidden.");

            Assert.That(
                Part("Journey Map").activeSelf,
                Is.False,
                "The journey map is still up after the menu was hidden, so it draws over "
                + "whatever the player entered. Its backdrop reference is most likely unset.");
        }

        [Test]
        public void ShowLevelMenu_BringsBothPiecesBack()
        {
            var view = instance.GetComponent<ApplicationUIView>();
            view.HideLevelMenu();
            view.ShowLevelMenu(OneUnlockedLevel(), _ => { });

            Assert.That(Part("Safe Area/Level Menu").activeSelf, Is.True);
            Assert.That(
                Part("Journey Map").activeSelf,
                Is.True,
                "The menu came back without its backdrop, so the chrome floats on nothing.");
        }

        private static IReadOnlyList<LevelMenuItemState> OneUnlockedLevel()
        {
            return new[]
            {
                new LevelMenuItemState(1, "Test Level", isUnlocked: true, isCleared: false, isBusy: false)
            };
        }

        private GameObject Part(string path)
        {
            Transform part = instance.transform.Find(path);
            Assert.That(part, Is.Not.Null, "The application UI prefab must author " + path + ".");
            return part.gameObject;
        }
    }
}
