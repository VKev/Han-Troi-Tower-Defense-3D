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
        public void RenderAndButton_ShowWaveStatusAndPublishStartRequest()
        {
            bool requested = false;
            view.StartWaveRequested += () => requested = true;
            view.Initialize();
            view.Render(new WaveHudState(
                "02 / 08",
                "READY TO START",
                0.25f,
                "07",
                "START WAVE",
                "+40 CLEAR BONUS",
                "• Speed Support  ×2 [SPEED AURA]",
                startWaveEnabled: true));

            Transform panel = owner.transform.Find("Safe Area/Tower Network HUD");
            Button button = panel.Find("Start Wave").GetComponent<Button>();
            Text preview = panel.Find("Wave Preview").GetComponent<Text>();
            Transform wavePanel = panel.Find("Wave Panel");

            Assert.That(button.interactable, Is.True);
            Assert.That(
                button.transform.Find("Label").GetComponent<Text>().text,
                Is.EqualTo("START WAVE"));
            Assert.That(
                button.transform.Find("Bonus").GetComponent<Text>().text,
                Is.EqualTo("+40 CLEAR BONUS"));
            Assert.That(
                wavePanel.Find("Wave Counter").GetComponent<Text>().text,
                Is.EqualTo("02 / 08"));
            Assert.That(
                wavePanel.Find("Wave Status").GetComponent<Text>().text,
                Is.EqualTo("READY TO START"));
            Assert.That(
                wavePanel.Find("Enemies Left").GetComponent<Text>().text,
                Is.EqualTo("07"));
            Assert.That(
                wavePanel.Find("Wave Progress Background/Wave Progress Fill")
                    .GetComponent<Image>()
                    .fillAmount,
                Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(preview.text, Does.Contain("[SPEED AURA]"));

            button.onClick.Invoke();

            Assert.That(requested, Is.True);
        }
    }
}
