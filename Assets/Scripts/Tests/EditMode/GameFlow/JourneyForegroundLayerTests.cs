using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow.Tests.EditMode
{
    /// <summary>
    /// A backdrop layer can sit in front of the trail instead of behind it. Two things have to
    /// hold for that to read as foreground rather than as a mistake: it has to draw over the
    /// safe-area chrome, and it has to slide further than the trail rather than less.
    ///
    /// The thing worth guarding hardest is that hanging a layer in front does not disturb the
    /// layers behind it. The depth ramp spreads its speeds across however many layers it is
    /// given, so were the foreground counted among them every band behind it would quietly slow
    /// down and the hand-tuned backdrop would have to be redone.
    /// </summary>
    public sealed class JourneyForegroundLayerTests
    {
        private const string ApplicationUiPrefabPath = "Assets/Resources/Prefabs/ApplicationUI.prefab";

        /// <summary>The authored defaults on the component, which this rig leaves alone.</summary>
        private const float SlowestFactor = 0.08f;
        private const float FastestFactor = 0.5f;
        private const float ForegroundFactor = 1.3f;

        /// <summary>
        /// The silhouette occupies rows 96..263 of its 384-tall sheet, so its bottom edge is a
        /// quarter of the way up the rect rather than at the rect's own bottom.
        /// </summary>
        private const float ArtBottomShareOfRect = 0.25f;

        /// <summary>Just below the screen, so the band is cut off rather than left floating.</summary>
        private const float ArtBottomBelowCanvas = -13f;

        /// <summary>
        /// The shape the art was drawn for, a 4:3 tablet, and a wide phone - the canvas sizes the
        /// scaler lands on for each, not the displays' own pixels.
        /// </summary>
        private static readonly Vector2[] DisplayShapes =
        {
            new Vector2(1920f, 1080f),
            new Vector2(1663f, 1247f),
            new Vector2(2119f, 979f)
        };

        private Scene rig;
        private GameObject canvasOwner;
        private GameObject owner;
        private JourneyParallaxView parallax;
        private RectTransform content;

        [SetUp]
        public void SetUp()
        {
            // A preview scene of its own, rather than whichever scene happens to be open. An
            // object made in the open scene marks it dirty, and the next assembly reload then
            // stops to ask whether to save what is only test scaffolding.
            rig = EditorSceneManager.NewPreviewScene();

            // The map hangs under a canvas, and it has to here too: Unity only honours
            // overrideSorting on a canvas nested inside another one, and quietly clears it on a
            // root canvas. A rig without this parent would report every foreground as background.
            canvasOwner = new GameObject("ApplicationUI");
            EditorSceneManager.MoveGameObjectToScene(canvasOwner, rig);
            canvasOwner.AddComponent<Canvas>().sortingOrder = 100;

            owner = new GameObject("Journey Map");
            owner.AddComponent<RectTransform>().SetParent(canvasOwner.transform, false);
            parallax = owner.AddComponent<JourneyParallaxView>();

            var scrollOwner = new GameObject("Level Scroll");
            scrollOwner.AddComponent<RectTransform>().SetParent(owner.transform, false);
            ScrollRect scroll = scrollOwner.AddComponent<ScrollRect>();

            var contentOwner = new GameObject("Level List");
            content = contentOwner.AddComponent<RectTransform>();
            content.SetParent(scrollOwner.transform, false);
            scroll.content = content;
        }

        [TearDown]
        public void TearDown()
        {
            // Closing the preview scene takes everything in it, so nothing is left in the open
            // scene to outlive the fixture and confuse whatever runs next.
            if (rig.IsValid())
            {
                EditorSceneManager.ClosePreviewScene(rig);
            }

            canvasOwner = null;
            owner = null;
        }

        [Test]
        public void Collect_LeavesTheBackgroundRampAloneWhenAForegroundIsAdded()
        {
            AddLayer("Layer1", foreground: false);
            AddLayer("Layer2", foreground: false);
            AddLayer("Layer3", foreground: false);
            Collect();

            List<float> backgroundOnly = new(Factors());
            Assert.That(backgroundOnly, Has.Count.EqualTo(3));
            Assert.That(backgroundOnly[0], Is.EqualTo(SlowestFactor).Within(0.0001f));
            Assert.That(backgroundOnly[2], Is.EqualTo(FastestFactor).Within(0.0001f));

            AddLayer("Layer4", foreground: true);
            Collect();

            IReadOnlyList<float> withForeground = Factors();
            Assert.That(withForeground, Has.Count.EqualTo(4));
            for (int index = 0; index < backgroundOnly.Count; index++)
            {
                Assert.That(
                    withForeground[index],
                    Is.EqualTo(backgroundOnly[index]).Within(0.0001f),
                    "Adding a foreground layer changed the speed of background layer " + index
                    + ", so the ramp counted it as one of them.");
            }

            Assert.That(withForeground[3], Is.EqualTo(ForegroundFactor).Within(0.0001f));
        }

        /// <summary>
        /// A nested canvas is also how a layer gets a batch of its own, so only one that actually
        /// takes over sorting counts as foreground.
        /// </summary>
        [Test]
        public void Collect_TreatsACanvasThatDoesNotOverrideSortingAsBackground()
        {
            AddLayer("Layer1", foreground: false);
            RectTransform batched = AddLayer("Layer2", foreground: false);
            batched.gameObject.AddComponent<Canvas>().overrideSorting = false;
            Collect();

            IReadOnlyList<float> factors = Factors();
            Assert.That(factors, Has.Count.EqualTo(2));
            Assert.That(factors[1], Is.EqualTo(FastestFactor).Within(0.0001f));
        }

        [Test]
        public void Apply_SlidesAForegroundFurtherThanTheTrail()
        {
            RectTransform nearestBackground = AddLayer("Layer3", foreground: false);
            RectTransform foreground = AddLayer("Layer4", foreground: true);
            Collect();

            const float travel = -1000f;
            content.anchoredPosition = new Vector2(travel, 0f);
            Apply(travel);

            Assert.That(
                Mathf.Abs(foreground.anchoredPosition.x),
                Is.GreaterThan(Mathf.Abs(travel)),
                "A foreground layer that slides less than the trail reads as being behind it.");
            Assert.That(
                Mathf.Abs(foreground.anchoredPosition.x),
                Is.GreaterThan(Mathf.Abs(nearestBackground.anchoredPosition.x)),
                "The foreground has to outrun the nearest background layer.");
        }

        [Test]
        public void Layer4_IsAuthoredAsAForegroundOverTheChrome()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(ApplicationUiPrefabPath);
            try
            {
                var canvas = root.GetComponent<Canvas>();
                Transform band = root.transform.Find("Journey Map/Layer4");
                Assert.That(band, Is.Not.Null, "The journey map must author a Layer4 foreground.");

                var sorting = band.GetComponent<Canvas>();
                Assert.That(sorting, Is.Not.Null, "A foreground band needs its own sorting canvas.");
                Assert.That(
                    sorting.overrideSorting,
                    Is.True,
                    "Without overrideSorting the band draws in hierarchy order, which puts it "
                    + "behind the safe area.");
                Assert.That(
                    sorting.sortingOrder,
                    Is.GreaterThan(canvas.sortingOrder),
                    "The band has to sort above the canvas the chrome draws in.");

                // It sits over the buttons, so it must not take their taps.
                Assert.That(
                    band.GetComponent<Image>().raycastTarget,
                    Is.False,
                    "A foreground band that takes raycasts blocks the chrome underneath it.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// The foreground has to stand on the bottom edge of every display, not at a fixed
        /// distance from the middle of one.
        /// </summary>
        /// <remarks>
        /// The canvas scaler blends width and height, so the canvas is 1247 units tall on a 4:3
        /// tablet and 979 on a wide phone. A band measured from the centre therefore floated 70
        /// units above the tablet's bottom edge and was cut 64 into the phone's - the same offset
        /// landing in two different places. Measuring from the bottom anchor instead is what
        /// makes the one authored number right everywhere.
        /// </remarks>
        [Test]
        public void Layer4_StandsOnTheBottomEdgeOfEveryDisplayShape()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ApplicationUiPrefabPath);
            GameObject instance = Object.Instantiate(prefab);
            EditorSceneManager.MoveGameObjectToScene(instance, rig);

            // World space is the one render mode whose canvas takes the size it is given rather
            // than the size of the game view.
            instance.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            Object.DestroyImmediate(instance.GetComponent<CanvasScaler>());
            instance.transform.Find("Journey Map").gameObject.SetActive(true);

            var canvasRect = (RectTransform)instance.transform;
            var band = (RectTransform)instance.transform.Find("Journey Map/Layer4");
            Assert.That(band, Is.Not.Null, "The journey map must author a Layer4 foreground.");

            foreach (Vector2 shape in DisplayShapes)
            {
                canvasRect.anchorMin = new Vector2(0.5f, 0.5f);
                canvasRect.anchorMax = new Vector2(0.5f, 0.5f);
                canvasRect.sizeDelta = shape;
                Canvas.ForceUpdateCanvases();

                float rectHeight = band.rect.height * band.localScale.y;
                float artBottom = band.localPosition.y
                                  - band.pivot.y * rectHeight
                                  + ArtBottomShareOfRect * rectHeight
                                  + shape.y * 0.5f;

                Assert.That(
                    artBottom,
                    Is.EqualTo(ArtBottomBelowCanvas).Within(2f),
                    $"On a {shape.x:0}x{shape.y:0} canvas the silhouette bottom sits at "
                    + $"{artBottom:0.#} instead of {ArtBottomBelowCanvas:0.#}: "
                    + (artBottom > 0f
                        ? "it is floating above the bottom edge."
                        : "it is cut too far into the screen."));
            }
        }

        private RectTransform AddLayer(string name, bool foreground)
        {
            var layer = new GameObject(name);
            RectTransform rect = layer.AddComponent<RectTransform>();
            rect.SetParent(owner.transform, false);

            // No Image on purpose. The rig only needs something the parallax will take as a
            // layer, and leaving live uGUI graphics behind in the shared edit-mode scene is how
            // one fixture starts breaking the next.
            if (foreground)
            {
                Canvas sorting = layer.AddComponent<Canvas>();
                sorting.overrideSorting = true;
                sorting.sortingOrder = 101;
            }

            return rect;
        }

        private void Collect()
        {
            typeof(JourneyParallaxView)
                .GetMethod("Collect", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(parallax, null);
        }

        private void Apply(float travel)
        {
            typeof(JourneyParallaxView)
                .GetMethod("Apply", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(parallax, new object[] { travel });
        }

        private IReadOnlyList<float> Factors()
        {
            return (List<float>)typeof(JourneyParallaxView)
                .GetField("factors", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(parallax);
        }
    }
}
