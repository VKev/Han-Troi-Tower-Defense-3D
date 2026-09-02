using System;
using System.Collections.Generic;
using NUnit.Framework;
using TowerDefense3D.Economy;
using TowerDefense3D.Enemies;
using TowerDefense3D.Waves;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow.Tests.EditMode
{
    public sealed class LevelOutcomeHudTests
    {
        private const string GameplayUiPrefabPath = "Assets/Resources/Prefabs/GameplayUI.prefab";

        [Test]
        public void Presenter_StaysHiddenWhileTheLevelIsStillPlayable()
        {
            var waveSystem = new WaveSystemStub();
            var view = new LevelOutcomeHudViewStub();
            LevelOutcomeHudPresenter presenter = CreatePresenter(waveSystem, view, hasNextLevel: true);

            presenter.Connect();

            Assert.That(view.InitializeCount, Is.EqualTo(1));
            Assert.That(view.LastState.IsVisible, Is.False);

            waveSystem.Phase = WavePhase.Running;
            presenter.Refresh();

            Assert.That(view.LastState.IsVisible, Is.False);

            presenter.Disconnect();

            Assert.That(view.ShutdownCount, Is.EqualTo(1));
        }

        [Test]
        public void Presenter_ShowsVictoryWithNextLevelWhenALaterLevelExists()
        {
            var waveSystem = new WaveSystemStub();
            var goldSystem = new LevelGoldSystem(250);
            var healthSystem = new LevelBaseHealthSystem(10);
            var view = new LevelOutcomeHudViewStub();
            var presenter = new LevelOutcomeHudPresenter(waveSystem, goldSystem, healthSystem, view);
            presenter.BindLevel("Level 1", true, () => { }, () => { }, () => { });

            healthSystem.TakeDamage(3);
            waveSystem.Phase = WavePhase.Victory;
            presenter.Connect();

            Assert.That(view.LastState.IsVisible, Is.True);
            Assert.That(view.LastState.Outcome, Is.EqualTo(LevelOutcome.Victory));
            Assert.That(view.LastState.TitleText, Is.EqualTo("VICTORY"));
            Assert.That(view.LastState.SummaryText, Does.Contain("Level 1 cleared"));
            Assert.That(view.LastState.SummaryText, Does.Contain("7/10"));
            Assert.That(view.LastState.SummaryText, Does.Contain("250"));
            Assert.That(view.LastState.NextLevelVisible, Is.True);
        }

        [Test]
        public void Presenter_ShowsDefeatWithoutNextLevelEvenWhenALaterLevelExists()
        {
            var waveSystem = new WaveSystemStub { Phase = WavePhase.Defeat };
            var healthSystem = new LevelBaseHealthSystem(10);
            var view = new LevelOutcomeHudViewStub();
            var presenter = new LevelOutcomeHudPresenter(
                waveSystem,
                new LevelGoldSystem(40),
                healthSystem,
                view);
            int replayCount = 0;
            int nextLevelCount = 0;
            int returnCount = 0;
            presenter.BindLevel(
                "Level 2",
                true,
                () => replayCount++,
                () => nextLevelCount++,
                () => returnCount++);

            healthSystem.TakeDamage(10);
            presenter.Connect();

            Assert.That(view.LastState.IsVisible, Is.True);
            Assert.That(view.LastState.Outcome, Is.EqualTo(LevelOutcome.Defeat));
            Assert.That(view.LastState.TitleText, Is.EqualTo("DEFEAT"));
            Assert.That(view.LastState.SummaryText, Does.Contain("Level 2 lost"));
            Assert.That(view.LastState.SummaryText, Does.Contain("0/10"));
            Assert.That(
                view.LastState.NextLevelVisible,
                Is.False,
                "Defeat must only offer replay and level select.");

            view.RaisePlayAgain();
            view.RaiseNextLevel();
            view.RaiseReturnToLevelMenu();

            Assert.That(replayCount, Is.EqualTo(1));
            Assert.That(nextLevelCount, Is.Zero, "Next level must stay inert after a defeat.");
            Assert.That(returnCount, Is.EqualTo(1));

            presenter.Disconnect();
            view.RaisePlayAgain();

            Assert.That(replayCount, Is.EqualTo(1), "Disconnect must detach the view handlers.");
        }

        [Test]
        public void Presenter_HidesNextLevelOnVictoryWhenNoLaterLevelExists()
        {
            var waveSystem = new WaveSystemStub { Phase = WavePhase.Victory };
            var view = new LevelOutcomeHudViewStub();
            int nextLevelCount = 0;
            var presenter = new LevelOutcomeHudPresenter(
                waveSystem,
                new LevelGoldSystem(0),
                new LevelBaseHealthSystem(10),
                view);
            presenter.BindLevel("Level 9", false, () => { }, () => nextLevelCount++, () => { });

            presenter.Connect();
            view.RaiseNextLevel();

            Assert.That(view.LastState.NextLevelVisible, Is.False);
            Assert.That(nextLevelCount, Is.Zero);
        }

        [Test]
        public void Presenter_WaitsForFrogEscapeBeforeShowingVictory()
        {
            var waveSystem = new WaveSystemStub { Phase = WavePhase.Victory };
            var view = new LevelOutcomeHudViewStub();
            var escape = new VictoryEscapeViewStub();
            var presenter = new LevelOutcomeHudPresenter(
                waveSystem,
                new LevelGoldSystem(0),
                new LevelBaseHealthSystem(10),
                view,
                escape);
            int levelClearedCount = 0;
            presenter.BindLevel(
                "Level 1",
                false,
                () => { },
                () => { },
                () => { },
                () => levelClearedCount++);

            presenter.Connect();

            Assert.That(escape.PlayCount, Is.EqualTo(1));
            Assert.That(view.LastState.IsVisible, Is.False);
            Assert.That(levelClearedCount, Is.Zero);

            // The frog's escape spans many frames, and every one of them refreshes the HUD. None of
            // them may raise the panel, and none may restart the escape.
            presenter.Refresh();
            presenter.Refresh();

            Assert.That(escape.PlayCount, Is.EqualTo(1));
            Assert.That(view.LastState.IsVisible, Is.False);

            escape.Complete();

            Assert.That(view.LastState.IsVisible, Is.True);
            Assert.That(view.LastState.Outcome, Is.EqualTo(LevelOutcome.Victory));
            Assert.That(levelClearedCount, Is.EqualTo(1));
        }

        [Test]
        public void Prefab_AuthorsOneHiddenOutcomePanelWithThreeButtons()
        {
            GameObject owner = PrefabUtility.LoadPrefabContents(GameplayUiPrefabPath);
            try
            {
                var view = owner.GetComponentInChildren<LevelOutcomeHudView>(true);
                Assert.That(view, Is.Not.Null, "Gameplay UI prefab must author a LevelOutcomeHudView.");

                // Hangs off the canvas, not the safe area, so its dim runs edge to edge - see
                // FullBleedOverlayTests.
                Transform hud = owner.transform.Find("Outcome HUD");
                Assert.That(hud, Is.Not.Null);
                Assert.That(hud.gameObject.activeSelf, Is.True, "The view owner must stay active for injection.");
                Assert.That(
                    owner.transform.Find("Safe Area/Victory HUD"),
                    Is.Null,
                    "The superseded victory-only panel must be gone.");

                Transform root = hud.Find("Outcome Root");
                Assert.That(root, Is.Not.Null);
                Assert.That(root.gameObject.activeSelf, Is.False, "The panel must start hidden.");
                Assert.That(
                    root.GetComponent<Image>().raycastTarget,
                    Is.True,
                    "The overlay must block board input while it is shown.");

                Transform buttons = root.Find("Outcome Card/Outcome Buttons");
                Assert.That(buttons, Is.Not.Null);
                Assert.That(buttons.childCount, Is.EqualTo(3));
                Assert.That(buttons.GetChild(0).name, Is.EqualTo("Play Again"));
                Assert.That(buttons.GetChild(1).name, Is.EqualTo("Next Level"));
                Assert.That(buttons.GetChild(2).name, Is.EqualTo("Level Select"));
                foreach (Transform button in buttons)
                {
                    Assert.That(button.GetComponent<Button>(), Is.Not.Null, button.name);
                }

                view.Initialize();
                view.Render(new LevelOutcomeHudState(
                    true,
                    LevelOutcome.Defeat,
                    "DEFEAT",
                    "lost",
                    false));

                Assert.That(root.gameObject.activeSelf, Is.True);
                Assert.That(
                    buttons.Find("Next Level").gameObject.activeSelf,
                    Is.False,
                    "Defeat must hide the next-level button so the row re-flows.");
                view.Shutdown();
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(owner);
            }
        }

        [Test]
        public void LevelCatalog_TryGetNextLevel_ReturnsTheLowestLaterLevel()
        {
            var catalog = ScriptableObject.CreateInstance<LevelCatalog>();
            try
            {
                SetLevels(
                    catalog,
                    new LevelCatalogEntry(3, "Level 3", "Assets/Scenes/Levels/Level_003.unity"),
                    new LevelCatalogEntry(1, "Level 1", "Assets/Scenes/Levels/Level_001.unity"),
                    new LevelCatalogEntry(2, "Level 2", "Assets/Scenes/Levels/Level_002.unity"));

                Assert.That(catalog.TryGetNextLevel(1, out LevelCatalogEntry next), Is.True);
                Assert.That(next.LevelNumber, Is.EqualTo(2));
                Assert.That(catalog.TryGetNextLevel(2, out next), Is.True);
                Assert.That(next.LevelNumber, Is.EqualTo(3));
                Assert.That(catalog.TryGetNextLevel(3, out next), Is.False);
                Assert.That(next, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        private static LevelOutcomeHudPresenter CreatePresenter(
            IWaveSystem waveSystem,
            ILevelOutcomeHudView view,
            bool hasNextLevel)
        {
            var presenter = new LevelOutcomeHudPresenter(
                waveSystem,
                new LevelGoldSystem(100),
                new LevelBaseHealthSystem(10),
                view);
            presenter.BindLevel("Level 1", hasNextLevel, () => { }, () => { }, () => { });
            return presenter;
        }

        private static void SetLevels(LevelCatalog catalog, params LevelCatalogEntry[] entries)
        {
            var serialized = new SerializedObject(catalog);
            SerializedProperty levels = serialized.FindProperty("levels");
            levels.arraySize = entries.Length;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            for (int index = 0; index < entries.Length; index++)
            {
                LevelCatalogEntry entry = entries[index];
                SerializedProperty element = levels.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("levelNumber").intValue = entry.LevelNumber;
                element.FindPropertyRelative("displayName").stringValue = entry.DisplayName;
                element.FindPropertyRelative("scenePath").stringValue = entry.ScenePath;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private sealed class WaveSystemStub : IWaveSystem
        {
            public event Action StateChanged;

            public WavePhase Phase { get; set; } = WavePhase.Preparation;
            public bool IsRunning => Phase == WavePhase.Running;

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
                StateChanged?.Invoke();
                return true;
            }

            public void ForceVictory()
            {
                Phase = WavePhase.Victory;
                StateChanged?.Invoke();
            }
        }

        private sealed class LevelOutcomeHudViewStub : ILevelOutcomeHudView
        {
            public event Action PlayAgainRequested;
            public event Action NextLevelRequested;
            public event Action ReturnToLevelMenuRequested;

            public int InitializeCount { get; private set; }
            public int ShutdownCount { get; private set; }
            public LevelOutcomeHudState LastState { get; private set; }

            public void Initialize()
            {
                InitializeCount++;
            }

            public void Render(LevelOutcomeHudState state)
            {
                LastState = state;
            }

            public void Shutdown()
            {
                ShutdownCount++;
            }

            public void RaisePlayAgain()
            {
                PlayAgainRequested?.Invoke();
            }

            public void RaiseNextLevel()
            {
                NextLevelRequested?.Invoke();
            }

            public void RaiseReturnToLevelMenu()
            {
                ReturnToLevelMenuRequested?.Invoke();
            }
        }

        private sealed class VictoryEscapeViewStub : ILevelVictoryEscapeView
        {
            public event Action EscapeCompleted;

            public int PlayCount { get; private set; }

            public void PlayEscape()
            {
                PlayCount++;
            }

            public void Complete()
            {
                EscapeCompleted?.Invoke();
            }
        }
    }
}
