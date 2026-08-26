using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow.Tests.EditMode
{
    public sealed class WaveHudViewTests
    {
        private const string GameplayUiPrefabPath =
            "Assets/Resources/Prefabs/GameplayUI.prefab";
        private GameObject owner;
        private WaveHudView view;

        [SetUp]
        public void SetUp()
        {
            owner = PrefabUtility.LoadPrefabContents(GameplayUiPrefabPath);
            view = owner.GetComponentInChildren<WaveHudView>(true);
            Assert.That(view, Is.Not.Null);
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
        }

        [Test]
        public void RenderAndButton_ShowWavePreviewAndPublishStartRequest()
        {
            bool requested = false;
            view.StartWaveRequested += () => requested = true;
            view.Initialize();
            view.Render(new WaveHudState(
                "START WAVE 2/8",
                "Next: 2x Speed Support [SPEED AURA]",
                startWaveEnabled: true));

            Transform panel = owner.transform.Find("Safe Area/Tower Network HUD");
            Button button = panel.Find("Start Wave").GetComponent<Button>();
            Text preview = panel.Find("Wave Preview").GetComponent<Text>();

            Assert.That(button.interactable, Is.True);
            Assert.That(
                button.GetComponentInChildren<Text>().text,
                Is.EqualTo("START WAVE 2/8"));
            Assert.That(preview.text, Does.Contain("[SPEED AURA]"));

            button.onClick.Invoke();

            Assert.That(requested, Is.True);
        }
    }
}
