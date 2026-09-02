using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow.Tests.PlayMode
{
    /// <summary>
    /// The journey backdrop is painted bands, each with copies laid out sideways so the parallax
    /// has room to slide. Nothing computes their layout at runtime - the sizes and offsets are
    /// authored into the prefab - so what needs guarding is those authored numbers.
    ///
    /// Three ways they have gone wrong before, each of which shows as a visible tear:
    /// a band stretched to the canvas takes the display's proportions and squashes the painting;
    /// a copy placed a hand-measured distance away stops meeting its neighbour; and a copy left
    /// facing the same way as its neighbour puts two unlike edges together, because the paintings
    /// are not drawn to tile.
    /// </summary>
    public sealed class JourneyBackdropBandPlayModeTests
    {
        private const string ApplicationUiResourcePath = "Prefabs/ApplicationUI";

        /// <summary>Anything named this way is a wash, and washes are allowed to stretch.</summary>
        private const string WashPrefix = "Fog";

        /// <summary>The shape the art was drawn for, a 4:3 tablet, and an extra-wide phone.</summary>
        private static readonly Vector2[] CanvasShapes =
        {
            new Vector2(1920f, 1080f),
            new Vector2(1663f, 1247f),
            new Vector2(2400f, 1080f)
        };

        private GameObject instance;

        [TearDown]
        public void TearDown()
        {
            if (instance != null)
            {
                Object.DestroyImmediate(instance);
                instance = null;
            }
        }

        [UnityTest]
        public IEnumerator Bands_KeepTheirPaintedProportionsAtEveryCanvasShape()
        {
            yield return Build();

            for (int shape = 0; shape < CanvasShapes.Length; shape++)
            {
                yield return Resize(CanvasShapes[shape]);

                IReadOnlyList<RectTransform> bands = FindBands();
                Assert.That(bands, Is.Not.Empty, "The journey map must author at least one band.");
                for (int index = 0; index < bands.Count; index++)
                {
                    RectTransform band = bands[index];
                    Sprite sprite = band.GetComponent<Image>().sprite;
                    float nativeAspect = sprite.rect.width / sprite.rect.height;
                    float drawnAspect = band.rect.width / band.rect.height;

                    Assert.That(
                        drawnAspect,
                        Is.EqualTo(nativeAspect).Within(0.01f),
                        $"'{band.name}' is drawn at {drawnAspect:0.###} but painted at "
                        + $"{nativeAspect:0.###} on a {CanvasShapes[shape].x:0}x"
                        + $"{CanvasShapes[shape].y:0} canvas.");
                }
            }
        }

        [UnityTest]
        public IEnumerator BandCopies_MeetWithoutSeamsAndAlternateTheirFacing()
        {
            yield return Build();

            for (int shape = 0; shape < CanvasShapes.Length; shape++)
            {
                yield return Resize(CanvasShapes[shape]);

                IReadOnlyList<RectTransform> bands = FindBands();
                for (int index = 0; index < bands.Count; index++)
                {
                    RectTransform band = bands[index];
                    float width = band.rect.width;
                    Assert.That(width, Is.GreaterThan(0f));

                    for (int child = 0; child < band.childCount; child++)
                    {
                        if (band.GetChild(child) is not RectTransform copy)
                        {
                            continue;
                        }

                        Assert.That(
                            copy.rect.size,
                            Is.EqualTo(band.rect.size).Within(0.01f),
                            $"Copy '{copy.name}' is a different size from its band, so the two draw "
                            + "the painting at different scales and cannot line up.");

                        // A copy has to sit a whole number of band widths away, or its edge lands
                        // somewhere other than against its neighbour's.
                        float steps = copy.anchoredPosition.x / width;
                        int step = Mathf.RoundToInt(steps);
                        Assert.That(
                            steps,
                            Is.EqualTo(step).Within(0.001f),
                            $"Copy '{copy.name}' sits {copy.anchoredPosition.x:0.##} from its band, "
                            + $"which is {steps:0.###} of its {width:0} width - the fractional part "
                            + $"is the seam, on a {CanvasShapes[shape].x:0}x"
                            + $"{CanvasShapes[shape].y:0} canvas.");

                        // Every other copy faces the other way, so like edge meets like.
                        bool shouldMirror = Mathf.Abs(step) % 2 == 1;
                        Quaternion wanted = shouldMirror
                            ? Quaternion.Euler(0f, 180f, 0f)
                            : Quaternion.identity;
                        Assert.That(
                            Quaternion.Angle(copy.localRotation, wanted),
                            Is.LessThan(0.5f),
                            $"Copy '{copy.name}' at step {step} should be "
                            + (shouldMirror ? "mirrored" : "upright")
                            + $" but is turned {copy.localEulerAngles.y:0} degrees about Y.");
                    }
                }
            }
        }

        /// <summary>The sky is the map's own backdrop and is meant to fill whatever shape it lands in.</summary>
        [UnityTest]
        public IEnumerator Sky_IsStillAllowedToStretch()
        {
            yield return Build();
            yield return Resize(new Vector2(1663f, 1247f));

            var map = (RectTransform)instance.transform.Find("Journey Map");
            Assert.That(map.rect.width, Is.EqualTo(1663f).Within(1f));
            Assert.That(map.rect.height, Is.EqualTo(1247f).Within(1f));
        }

        private IEnumerator Build()
        {
            var prefab = Resources.Load<GameObject>(ApplicationUiResourcePath);
            Assert.That(prefab, Is.Not.Null, "Application UI prefab must live in Resources.");
            instance = Object.Instantiate(prefab);

            // World space, because that is the one render mode whose canvas takes the size it is
            // given rather than the size of the game view.
            instance.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            Object.DestroyImmediate(instance.GetComponent<CanvasScaler>());
            instance.transform.Find("Journey Map").gameObject.SetActive(true);
            yield return null;
        }

        private IEnumerator Resize(Vector2 size)
        {
            var canvasRect = (RectTransform)instance.transform;
            canvasRect.anchorMin = new Vector2(0.5f, 0.5f);
            canvasRect.anchorMax = new Vector2(0.5f, 0.5f);
            canvasRect.sizeDelta = size;
            Canvas.ForceUpdateCanvases();
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        /// <summary>
        /// A band is a painted layer of the map with copies of itself beside it: it carries a
        /// picture, it is not a wash, and it is not the scroll view the trail lives in.
        /// </summary>
        private IReadOnlyList<RectTransform> FindBands()
        {
            var bands = new List<RectTransform>();
            Transform map = instance.transform.Find("Journey Map");
            for (int index = 0; index < map.childCount; index++)
            {
                if (map.GetChild(index) is not RectTransform child)
                {
                    continue;
                }

                if (child.name.StartsWith(WashPrefix, System.StringComparison.OrdinalIgnoreCase)
                    || child.GetComponent<ScrollRect>() != null
                    || child.childCount == 0)
                {
                    continue;
                }

                Image image = child.GetComponent<Image>();
                if (image != null && image.sprite != null)
                {
                    bands.Add(child);
                }
            }

            return bands;
        }
    }
}
