using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow.Tests.EditMode
{
    /// <summary>
    /// A backdrop cloud is written by two things at once: the parallax slides it against the trail
    /// and the drift wanders it on its own clock. Both write one anchoredPosition, so the only way
    /// both can be seen is for the parallax to add the drift's offset to the slide it writes.
    ///
    /// What this guards is that the offset is added rather than either effect quietly winning: a
    /// cloud that only parallaxes looks frozen while the map is idle, and a cloud that only drifts
    /// falls out of the depth ramp and reads as pasted on top of the backdrop.
    /// </summary>
    public sealed class JourneyCloudDriftTests
    {
        /// <summary>The authored default on the parallax, which a lone background layer takes.</summary>
        private const float SlowestFactor = 0.08f;

        private const float Speed = 10f;
        private const float Distance = 100f;

        private Scene rig;
        private GameObject canvasOwner;
        private GameObject owner;
        private JourneyParallaxView parallax;
        private RectTransform content;

        [SetUp]
        public void SetUp()
        {
            // A preview scene of its own, so nothing here dirties whichever scene happens to be
            // open and stops the next assembly reload to ask about saving test scaffolding.
            rig = EditorSceneManager.NewPreviewScene();

            canvasOwner = new GameObject("ApplicationUI");
            EditorSceneManager.MoveGameObjectToScene(canvasOwner, rig);
            canvasOwner.AddComponent<Canvas>();

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
            if (rig.IsValid())
            {
                EditorSceneManager.ClosePreviewScene(rig);
            }

            canvasOwner = null;
            owner = null;
        }

        [Test]
        public void Apply_AddsTheDriftOnTopOfTheParallaxSlide()
        {
            var home = new Vector2(200f, -24f);
            CloudDriftView drift = AddCloud("Cloud", home, CloudDriftView.DriftDirection.Right);
            Collect();

            // Half the outbound leg, so the cloud is half its drift distance from home.
            SetElapsed(drift, Distance / Speed * 0.5f);

            const float travel = -1000f;
            content.anchoredPosition = new Vector2(travel, 0f);
            Apply(travel);

            float expected = home.x + travel * SlowestFactor + Distance * 0.5f;
            Assert.That(
                Cloud("Cloud").anchoredPosition.x,
                Is.EqualTo(expected).Within(0.001f),
                "The cloud has to carry both its parallax slide and its drift, not one of them.");
        }

        [Test]
        public void Apply_SendsTheDriftTheWayTheCloudWasPointed()
        {
            var home = new Vector2(200f, -24f);
            CloudDriftView drift = AddCloud("Cloud", home, CloudDriftView.DriftDirection.Left);
            Collect();
            SetElapsed(drift, Distance / Speed);

            Apply(0f);

            Assert.That(
                Cloud("Cloud").anchoredPosition.x,
                Is.EqualTo(home.x - Distance).Within(0.001f),
                "A cloud pointed left drifted right.");
        }

        /// <summary>
        /// The drift writes the position itself when nothing else owns the cloud. Being collected
        /// as a parallax layer has to take that over, or the two would take turns overwriting each
        /// other and which one showed would come down to script execution order.
        /// </summary>
        [Test]
        public void Collect_TakesOverTheCloudSoTheDriftStopsWritingItToo()
        {
            CloudDriftView drift = AddCloud(
                "Cloud",
                new Vector2(200f, -24f),
                CloudDriftView.DriftDirection.Right);

            Assert.That(IsDrivenExternally(drift), Is.False, "Nothing owns the cloud yet.");
            Collect();
            Assert.That(IsDrivenExternally(drift), Is.True);
        }

        /// <summary>
        /// The parallax stops re-applying once the trail settles, which is the right call while
        /// every layer is still. A drifting layer is not still, and the early-out has to know it.
        /// </summary>
        [Test]
        public void Collect_NotesThatALayerDrifts()
        {
            AddLayer("Layer1", Vector2.zero);
            Collect();
            Assert.That(HasDrift(), Is.False, "A map of plain layers must keep its idle early-out.");

            AddCloud("Cloud", new Vector2(200f, -24f), CloudDriftView.DriftDirection.Right);
            Collect();
            Assert.That(HasDrift(), Is.True);
        }

        private RectTransform AddLayer(string name, Vector2 home)
        {
            var layer = new GameObject(name);
            RectTransform rect = layer.AddComponent<RectTransform>();
            rect.SetParent(owner.transform, false);
            rect.anchoredPosition = home;
            return rect;
        }

        private CloudDriftView AddCloud(
            string name,
            Vector2 home,
            CloudDriftView.DriftDirection direction)
        {
            RectTransform rect = AddLayer(name, home);
            CloudDriftView drift = rect.gameObject.AddComponent<CloudDriftView>();

            SetField(drift, "direction", direction);
            SetField(drift, "speed", Speed);
            SetField(drift, "distance", Distance);
            SetField(drift, "holdSeconds", 0f);
            SetField(drift, "phase", 0f);

            // Awake does not run in edit mode, and it is what the component reads its authored
            // spot in. The parallax keeps the home it applies against, so only the drift's own
            // record of it has to be stood up here.
            SetField(drift, "homePosition", home);

            return drift;
        }

        private RectTransform Cloud(string name)
        {
            return (RectTransform)owner.transform.Find(name);
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
        }

        private static void SetElapsed(CloudDriftView drift, float seconds)
        {
            SetField(drift, "elapsedSeconds", seconds);
        }

        private static bool IsDrivenExternally(CloudDriftView drift)
        {
            return (bool)typeof(CloudDriftView)
                .GetField("drivenExternally", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(drift);
        }

        private bool HasDrift()
        {
            return (bool)typeof(JourneyParallaxView)
                .GetField("hasDrift", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(parallax);
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
    }
}
