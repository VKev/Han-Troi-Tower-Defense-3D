using System;
using System.Collections.Generic;
using NUnit.Framework;
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

        [Test]
        public void Prefab_AuthorsTheSkipCheatButtonBesideTheMenuButton()
        {
            GameObject owner = PrefabUtility.LoadPrefabContents(GameplayUiPrefabPath);
            try
            {
                Transform safeArea = owner.transform.Find("Safe Area");
                Transform skip = safeArea.Find("Skip Waves Cheat");
                Assert.That(skip, Is.Not.Null, "Gameplay UI prefab must author the skip cheat.");
                Assert.That(skip.gameObject.activeSelf, Is.True);

                var view = skip.GetComponent<LevelSkipCheatView>();
                Assert.That(view, Is.Not.Null);

                var serialized = new SerializedObject(view);
                Assert.That(
                    serialized.FindProperty("skipButton").objectReferenceValue,
                    Is.SameAs(skip.GetComponent<Button>()),
                    "The view must be wired to its own button.");

                Transform menu = safeArea.Find("Return To Level Menu");
                Assert.That(menu, Is.Not.Null);
                var skipRect = (RectTransform)skip;
                var menuRect = (RectTransform)menu;
                Assert.That(
                    skipRect.anchoredPosition.y,
                    Is.EqualTo(menuRect.anchoredPosition.y).Within(0.01f),
                    "The cheat must sit on the same row as MENU.");
                Assert.That(
                    skipRect.anchoredPosition.x,
                    Is.LessThan(menuRect.anchoredPosition.x - menuRect.sizeDelta.x),
                    "The cheat must sit clear of MENU rather than overlapping it.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(owner);
            }
        }

        private sealed class SkipCheatViewStub : ILevelSkipCheatView
        {
            public event Action SkipToVictoryRequested;

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

            public void ForceVictory()
            {
                ForceVictoryCount++;
                Phase = WavePhase.Victory;
                StateChanged?.Invoke();
            }
        }
    }
}
