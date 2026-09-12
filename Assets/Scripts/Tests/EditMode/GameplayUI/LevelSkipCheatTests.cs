using System;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using TowerDefense3D.Waves;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow.Tests.EditMode
{
    public sealed class LevelSkipCheatTests
    {
        private const string GameplayUiPrefabPath = "Assets/Resources/Prefabs/GameplayUI.prefab";

        [Test]
        public void Presenter_ForcesVictoryOnceAndThenGoesInert()
        {
            var waveSystem = new WaveSystemStub();
            var view = new SkipCheatViewStub();
            var presenter = new LevelSkipCheatPresenter(waveSystem, view);

            presenter.Connect();

            Assert.That(view.InitializeCount, Is.EqualTo(1));
            Assert.That(view.ShowCount, Is.EqualTo(1));
            Assert.That(view.LastCanSkip, Is.True, "A playable level must offer the cheat.");

            view.RaiseSkipToVictory();

            Assert.That(waveSystem.ForceVictoryCount, Is.EqualTo(1));
            Assert.That(
                view.LastCanSkip,
                Is.False,
                "The cheat must stop offering itself once the level is over.");

            view.RaiseSkipToVictory();

            Assert.That(
                waveSystem.ForceVictoryCount,
                Is.EqualTo(1),
                "A second press must not re-run the victory flow.");

            presenter.Disconnect();
            view.RaiseSkipToVictory();

            Assert.That(view.ShutdownCount, Is.EqualTo(1));
            Assert.That(waveSystem.ForceVictoryCount, Is.EqualTo(1));
        }

        [Test]
        public void Presenter_KeepsTheCheatInertAfterADefeat()
        {
            var waveSystem = new WaveSystemStub { Phase = WavePhase.Defeat };
            var view = new SkipCheatViewStub();
            var presenter = new LevelSkipCheatPresenter(waveSystem, view);

            presenter.Connect();
            view.RaiseSkipToVictory();

            Assert.That(view.LastCanSkip, Is.False);
            Assert.That(waveSystem.ForceVictoryCount, Is.Zero);
        }

        /// <summary>
        /// The skip cheat is drawn invisibly, and stays hittable anyway.
        /// </summary>
        /// <remarks>
        /// It is a debug shortcut rather than part of the game, so it is authored transparent -
        /// the same bargain the frame-rate overlay makes - and the developer who wants it taps the
        /// corner it sits in. Transparent is not the same as unhittable, and what is worth holding
        /// here is everything that decides whether the tap lands: it is authored, wired to its own
        /// button, and nothing about how it is drawn rejects the touch.
        /// </remarks>
        [Test]
        public void Prefab_AuthorsTheSkipCheatInvisibleButStillHittable()
        {
            GameObject owner = PrefabUtility.LoadPrefabContents(GameplayUiPrefabPath);
            try
            {
                Transform safeArea = owner.transform.Find("Safe Area");
                Transform skip = safeArea.Find("Skip Waves Cheat");
                Assert.That(skip, Is.Not.Null, "Gameplay UI prefab must author the skip cheat.");

                var view = skip.GetComponent<LevelSkipCheatView>();
                Assert.That(view, Is.Not.Null);

                var serialized = new SerializedObject(view);
                Assert.That(
                    serialized.FindProperty("skipButton").objectReferenceValue,
                    Is.SameAs(skip.GetComponent<Button>()),
                    "The view must be wired to its own button.");

                Assert.That(
                    safeArea.Find("Return To Level Menu"),
                    Is.Null,
                    "The HUD's menu button is gone; the pause modal carries that command.");

                // Transparent, but transparent is not the same as unhittable: a Graphic that is
                // enabled, raycast-targeted and not alpha-thresholded still takes the tap.
                var background = skip.GetComponent<Image>();
                Assert.That(background.enabled, Is.True, "A disabled Graphic cannot be hit.");
                Assert.That(background.raycastTarget, Is.True);
                Assert.That(
                    background.alphaHitTestMinimumThreshold,
                    Is.Zero,
                    "A threshold above zero would reject the tap on a transparent button.");
                Assert.That(
                    skip.Find("Label").GetComponent<TMP_Text>().color.a,
                    Is.Zero,
                    "The label must not be drawn either.");

                var skipRect = (RectTransform)skip;
                Assert.That(skipRect.sizeDelta.x, Is.GreaterThan(0f));
                Assert.That(skipRect.sizeDelta.y, Is.GreaterThan(0f));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(owner);
            }
        }

        private sealed class SkipCheatViewStub : ILevelSkipCheatView
        {
            public event Action SkipToVictoryRequested;

            public event Action SkipWaveRequested;

            public int InitializeCount { get; private set; }
            public int ShowCount { get; private set; }
            public int ShutdownCount { get; private set; }
            public bool LastCanSkip { get; private set; }

            public void Initialize()
            {
                InitializeCount++;
            }

            public void Render(bool canSkip)
            {
                LastCanSkip = canSkip;
            }

            public void Show()
            {
                ShowCount++;
            }

            public void Shutdown()
            {
                ShutdownCount++;
            }

            public void RaiseSkipToVictory()
            {
                SkipToVictoryRequested?.Invoke();
            }
        }

        private sealed class WaveSystemStub : IWaveSystem
        {
            public event Action StateChanged;

            public WavePhase Phase { get; set; } = WavePhase.Preparation;
            public bool IsRunning => Phase == WavePhase.Running;
            public bool IsCurrentWaveRetryAvailable => false;
            public bool HasRetriedCurrentWave => false;
            public int ForceVictoryCount { get; private set; }

            public WaveState CreateState()
            {
                return new WaveState(Phase, 1, 1, 0, false, 0);
            }

            public IReadOnlyList<EnemySpawnBatchDefinition> GetNextWavePreview()
            {
                return Array.Empty<EnemySpawnBatchDefinition>();
            }

            public bool TryStartWave(out string error)
            {
                error = string.Empty;
                return true;
            }

            public bool RetryCurrentWave() => false;

            public void ForceVictory()
            {
                ForceVictoryCount++;
                Phase = WavePhase.Victory;
                StateChanged?.Invoke();
            }

            public int ForceSkipWaveCount { get; private set; }

            public void ForceSkipWave()
            {
                ForceSkipWaveCount++;
                Phase = WavePhase.Preparation;
                StateChanged?.Invoke();
            }
        }
    }
}
