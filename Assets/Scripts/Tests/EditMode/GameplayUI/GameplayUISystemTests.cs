using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TowerDefense3D.Economy;
using TowerDefense3D.Enemies;
using TowerDefense3D.GameplayInput;
using TowerDefense3D.GridPlacement;
using TowerDefense3D.Tutorials;
using TowerDefense3D.Towers;
using TowerDefense3D.Waves;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.GameFlow.Tests.EditMode
{
    public sealed class GameplayUISystemTests
    {
        private const string BoardPath = "Assets/Config/GridPlacement/Level_001_Board.asset";
        private const string EnemyPath = "Assets/Config/Enemies/Basic.asset";
        private const string TowerCatalogPath = "Assets/Config/Towers/Catalogs/TowerCatalog.asset";

        [Test]
        public void RefreshIfDirty_RefreshesForLocalStateEventsAndRoutesViewCommands()
        {
            TowerNetworkSystem towerNetworkSystem = CreateTowerNetworkSystem();
            var goldSystem = new LevelGoldSystem(400);
            var healthSystem = new LevelBaseHealthSystem(10);
            var enemySystem = new EnemySystem(
                new RoadPath(new[] { Vector3.zero, Vector3.forward }),
                goldSystem,
                healthSystem);
            EnemyDefinition enemyDefinition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(EnemyPath);
            Assert.That(enemyDefinition, Is.Not.Null);
            var gameplayView = new GameplayViewStub();
            var towerHudView = new TowerNetworkHudViewStub();
            TowerCatalog towerCatalog = AssetDatabase.LoadAssetAtPath<TowerCatalog>(TowerCatalogPath);
            var presenter = new TowerNetworkHudPresenter(
                towerNetworkSystem,
                towerHudView,
                null,
                towerCatalog,
                CreateSaveSystem());
            var waveSystem = new WaveSystemStub();
            var waveHudView = new WaveHudViewStub();
            var wavePresenter = new WaveHudPresenter(waveSystem, waveHudView);
            var statusHudView = new LevelStatusHudViewStub();
            var gameplayUISystem = new GameplayUISystem(
                gameplayView,
                towerNetworkSystem,
                presenter,
                waveSystem,
                wavePresenter,
                enemySystem,
                goldSystem,
                healthSystem,
                statusHudView);
            int returnRequestCount = 0;
            gameplayUISystem.BindReturnToMenu(() => returnRequestCount++);

            towerNetworkSystem.Start();
            gameplayUISystem.Start();
            gameplayUISystem.RefreshIfDirty();
            gameplayUISystem.RefreshIfDirty();

            Assert.That(gameplayView.ShowCount, Is.EqualTo(1));
            Assert.That(towerHudView.InitializeCount, Is.EqualTo(1));
            Assert.That(towerHudView.ShowCount, Is.EqualTo(1));
            Assert.That(towerHudView.RenderCount, Is.EqualTo(1));
            Assert.That(waveHudView.ShowCount, Is.EqualTo(1));
            Assert.That(waveHudView.RenderCount, Is.EqualTo(1));
            Assert.That(statusHudView.LastGold, Is.EqualTo(400));
            Assert.That(statusHudView.LastHealth, Is.EqualTo(10));
            Assert.That(statusHudView.LastMaximumHealth, Is.EqualTo(10));

            towerNetworkSystem.ReportFeedback("Network changed.");
            gameplayUISystem.RefreshIfDirty();

            Assert.That(towerHudView.RenderCount, Is.EqualTo(2));
            Assert.That(towerHudView.LastState.FeedbackText, Is.EqualTo("Network changed."));

            waveSystem.PublishStateChanged();
            gameplayUISystem.RefreshIfDirty();

            Assert.That(waveHudView.RenderCount, Is.EqualTo(3));

            enemySystem.Spawn(enemyDefinition);
            gameplayUISystem.RefreshIfDirty();

            Assert.That(waveHudView.RenderCount, Is.EqualTo(4));

            goldSystem.Add(10);
            healthSystem.TakeDamage(1);

            Assert.That(statusHudView.LastGold, Is.EqualTo(410));
            Assert.That(statusHudView.LastHealth, Is.EqualTo(9));

            towerHudView.RequestReturnToMenu();

            Assert.That(returnRequestCount, Is.EqualTo(1));

            gameplayUISystem.Dispose();
            towerNetworkSystem.ReportFeedback("Ignored after disposal.");
            gameplayUISystem.RefreshIfDirty();

            Assert.That(towerHudView.RenderCount, Is.EqualTo(4));
            towerNetworkSystem.Dispose();
        }

        /// <summary>
        /// The skip cheat only calls ForceVictory; everything the player then sees comes out of
        /// the real phase machine, so this walks that machine rather than a stub of it.
        /// </summary>
        [Test]
        public void WaveSystem_ForceVictory_ReportsEveryWaveBeatenFromAnyPlayablePhase()
        {
            TowerNetworkSystem towerNetworkSystem = CreateTowerNetworkSystem();
            var enemyDefinition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(EnemyPath);
            Assert.That(enemyDefinition, Is.Not.Null);
            WaveScheduleDefinition schedule =
                ScriptableObject.CreateInstance<WaveScheduleDefinition>();
            var towerOwner = new GameObject("Skip Cheat Towers");
            try
            {
                ConfigureSchedule(schedule, enemyDefinition);
                var goldSystem = new LevelGoldSystem(1000);
                var healthSystem = new LevelBaseHealthSystem(10);
                var waveSystem = new WaveSystem(
                    schedule,
                    new EnemySystem(
                        new RoadPath(new[] { Vector3.zero, Vector3.forward }),
                        goldSystem,
                        healthSystem),
                    towerNetworkSystem,
                    new WaveSpawnPlanner(),
                    goldSystem,
                    healthSystem);
                int stateChangedCount = 0;
                waveSystem.StateChanged += () => stateChangedCount++;
                towerNetworkSystem.Start();

                Assert.That(waveSystem.CreateState().Phase, Is.EqualTo(WavePhase.Preparation));

                waveSystem.ForceVictory();

                WaveState state = waveSystem.CreateState();
                Assert.That(state.Phase, Is.EqualTo(WavePhase.Victory));
                Assert.That(
                    state.CurrentWaveNumber,
                    Is.EqualTo(state.WaveCount),
                    "Skipping must report every authored wave as beaten.");
                Assert.That(state.LivingEnemyCount, Is.Zero, "The board must be cleared.");
                Assert.That(waveSystem.GetNextWavePreview(), Is.Empty);
                Assert.That(stateChangedCount, Is.EqualTo(1), "The HUD must be told once.");
                Assert.That(waveSystem.TryStartWave(out string error), Is.False);
                Assert.That(error, Is.Not.Empty);

                waveSystem.ForceVictory();

                Assert.That(
                    stateChangedCount,
                    Is.EqualTo(1),
                    "Skipping an already-won level must be a no-op.");

                // Replaying the level has to leave the cheat usable again, and the cheat also has
                // to work from the middle of a running wave, which is when it is actually pressed.
                waveSystem.Reset();
                BuildValidTowerChain(towerNetworkSystem, towerOwner);
                Assert.That(waveSystem.TryStartWave(out error), Is.True, error);
                Assert.That(waveSystem.CreateState().Phase, Is.EqualTo(WavePhase.Running));
                waveSystem.StepSpawning(2f);
                Assert.That(
                    waveSystem.CreateState().LivingEnemyCount,
                    Is.GreaterThan(0),
                    "The wave must have enemies on the board for the skip to have work to do.");

                waveSystem.ForceVictory();

                Assert.That(waveSystem.CreateState().Phase, Is.EqualTo(WavePhase.Victory));
                Assert.That(
                    waveSystem.CreateState().LivingEnemyCount,
                    Is.Zero,
                    "Skipping mid-wave must sweep the enemies still walking the road.");
            }
            finally
            {
                towerNetworkSystem.Dispose();
                UnityEngine.Object.DestroyImmediate(towerOwner);
                UnityEngine.Object.DestroyImmediate(schedule);
            }
        }

        [Test]
        public void LevelOnePreparation_UnlocksTutorialCardsAtTheirAssignedWaves()
        {
            TowerCatalog towerCatalog = AssetDatabase.LoadAssetAtPath<TowerCatalog>(TowerCatalogPath);
            var towerHudView = new TowerNetworkHudViewStub();
            var waveSystem = new WaveSystemStub();
            var presenter = new TowerNetworkHudPresenter(
                CreateTowerNetworkSystem(),
                towerHudView,
                null,
                towerCatalog,
                CreateSaveSystem(),
                new TutorialProgress(),
                levelNumber: 1,
                waveSystem: waveSystem);

            presenter.Connect();
            AssertCardUnlocks(towerHudView, towerCatalog, expectSink: false, expectFire: false);

            waveSystem.CurrentWaveNumber = 3;
            presenter.Refresh();
            AssertCardUnlocks(towerHudView, towerCatalog, expectSink: true, expectFire: false);

            waveSystem.CurrentWaveNumber = 4;
            presenter.Refresh();
            AssertCardUnlocks(towerHudView, towerCatalog, expectSink: true, expectFire: true);
        }

        [Test]
        public void WaveThreePreparation_ReservesGoldForFireBeforeAllowingAnotherGenerator()
        {
            TowerCatalog towerCatalog = AssetDatabase.LoadAssetAtPath<TowerCatalog>(TowerCatalogPath);
            var tutorialProgress = new TutorialProgress();
            tutorialProgress.MarkCompleted("second_wave_expansion_v1");
            var waveSystem = new WaveSystemStub { CurrentWaveNumber = 3 };
            var goldSystem = new LevelGoldSystem(300);
            var towerHudView = new TowerNetworkHudViewStub();
            var presenter = new TowerNetworkHudPresenter(
                CreateTowerNetworkSystem(),
                towerHudView,
                null,
                towerCatalog,
                CreateSaveSystem(),
                tutorialProgress,
                levelNumber: 1,
                waveSystem: waveSystem,
                goldSystem: goldSystem);

            presenter.Connect();

            Assert.That(IsLocked(towerHudView, FindDefinition(towerCatalog, TowerFamily.Generator)), Is.True);
            Assert.That(IsLocked(towerHudView, FindDefinition(towerCatalog, TowerFamily.SoulNexus)), Is.True);

            goldSystem.Add(200);
            presenter.Refresh();

            Assert.That(IsLocked(towerHudView, FindDefinition(towerCatalog, TowerFamily.Generator)), Is.False);
            Assert.That(IsLocked(towerHudView, FindDefinition(towerCatalog, TowerFamily.SoulNexus)), Is.False);
        }

        [Test]
        public void WaveSixPreparation_UnlocksWaterAfterLevelOneTutorial()
        {
            TowerCatalog towerCatalog = AssetDatabase.LoadAssetAtPath<TowerCatalog>(TowerCatalogPath);
            var tutorialProgress = new TutorialProgress();
            tutorialProgress.CompleteLevelOneTutorial();
            var towerHudView = new TowerNetworkHudViewStub();
            var presenter = new TowerNetworkHudPresenter(
                CreateTowerNetworkSystem(),
                towerHudView,
                null,
                towerCatalog,
                CreateSaveSystem(),
                tutorialProgress,
                levelNumber: 1,
                waveSystem: new WaveSystemStub { CurrentWaveNumber = 6 });

            presenter.Connect();

            Assert.That(IsLocked(towerHudView, FindDefinition(towerCatalog, TowerFamily.Water)), Is.False);
        }

        private static void AssertCardUnlocks(
            TowerNetworkHudViewStub view,
            TowerCatalog catalog,
            bool expectSink,
            bool expectFire)
        {
            Assert.That(IsLocked(view, FindDefinition(catalog, TowerFamily.Generator)), Is.False);
            Assert.That(IsLocked(view, FindDefinition(catalog, TowerFamily.SoulNexus)), Is.EqualTo(!expectSink));
            Assert.That(IsLocked(view, FindDefinition(catalog, TowerFamily.Fire)), Is.EqualTo(!expectFire));
        }

        private static bool IsLocked(TowerNetworkHudViewStub view, TowerCombatDefinition definition)
        {
            for (int index = 0; index < view.LastLockedDefinitions.Count; index++)
            {
                if (view.LastLockedDefinitions[index] == definition) return true;
            }

            return false;
        }

        private static TowerCombatDefinition FindDefinition(TowerCatalog catalog, TowerFamily family)
        {
            for (int index = 0; index < catalog.Definitions.Count; index++)
            {
                TowerCombatDefinition definition = catalog.Definitions[index];
                if (definition.Family == family) return definition;
            }

            return null;
        }

        [Test]
        public void WaveSystem_WaveThreeLeak_DefeatsAndRetriesOnlyCurrentWave()
        {
            TowerNetworkSystem towerNetworkSystem = CreateTowerNetworkSystem();
            var enemyDefinition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(EnemyPath);
            var schedule = ScriptableObject.CreateInstance<WaveScheduleDefinition>();
            var towerOwner = new GameObject("Wave Three Retry Towers");
            try
            {
                ConfigureSchedule(schedule, enemyDefinition, 3);
                var goldSystem = new LevelGoldSystem(1000);
                var healthSystem = new LevelBaseHealthSystem(10);
                var enemySystem = new EnemySystem(
                    new RoadPath(new[] { Vector3.zero, Vector3.forward }),
                    goldSystem,
                    healthSystem);
                var waveSystem = new WaveSystem(
                    schedule,
                    enemySystem,
                    towerNetworkSystem,
                    new WaveSpawnPlanner(),
                    goldSystem,
                    healthSystem,
                    instantDefeatWaveNumber: 3);
                towerNetworkSystem.Start();
                BuildValidTowerChain(towerNetworkSystem, towerOwner);
                waveSystem.ForceSkipWave();
                waveSystem.ForceSkipWave();
                int checkpointGold = goldSystem.Balance;
                int checkpointHealth = healthSystem.CurrentHealth;

                Assert.That(waveSystem.CurrentWaveNumber, Is.EqualTo(3));
                Assert.That(waveSystem.TryStartWave(out string error), Is.True, error);
                waveSystem.StepSpawning(2f);
                enemySystem.Step(100f);

                Assert.That(waveSystem.CreateState().Phase, Is.EqualTo(WavePhase.Defeat));
                Assert.That(waveSystem.CanRetryCurrentWave, Is.True);
                Assert.That(
                    healthSystem.CurrentHealth,
                    Is.EqualTo(checkpointHealth - enemyDefinition.LeakDamage),
                    "Only the first Wave 3 leak may damage the Cóc before defeat freezes the wave.");
                goldSystem.Add(25);

                Assert.That(waveSystem.RetryCurrentWave(), Is.True);
                Assert.That(waveSystem.CreateState().Phase, Is.EqualTo(WavePhase.Preparation));
                Assert.That(waveSystem.CurrentWaveNumber, Is.EqualTo(3));
                Assert.That(waveSystem.CreateState().LivingEnemyCount, Is.Zero);
                Assert.That(healthSystem.CurrentHealth, Is.EqualTo(checkpointHealth));
                Assert.That(goldSystem.Balance, Is.EqualTo(checkpointGold));
            }
            finally
            {
                towerNetworkSystem.Dispose();
                UnityEngine.Object.DestroyImmediate(towerOwner);
                UnityEngine.Object.DestroyImmediate(schedule);
            }
        }

        /// <summary>
        /// The smallest network a wave will start on: one Generator wired to one Soul Nexus.
        /// Placement is driven through the system's own placement callback because that is the
        /// only path that registers a tower with the network.
        /// </summary>
        private static void BuildValidTowerChain(TowerNetworkSystem system, GameObject owner)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<TowerCatalog>(TowerCatalogPath);
            TowerRuntimeView generator = RegisterTower(
                system,
                catalog,
                TowerFamily.Generator,
                Vector3.zero,
                1,
                owner);
            TowerRuntimeView nexus = RegisterTower(
                system,
                catalog,
                TowerFamily.SoulNexus,
                Vector3.right,
                2,
                owner);
            Assert.That(system.TryRewire(generator, nexus, out string error), Is.True, error);
        }

        private static TowerRuntimeView RegisterTower(
            TowerNetworkSystem system,
            TowerCatalog catalog,
            TowerFamily family,
            Vector3 position,
            int ownerId,
            GameObject owner)
        {
            Assert.That(catalog.TryGet(family, out TowerCombatDefinition definition), Is.True);
            var towerObject = new GameObject(family + " Test Tower");
            towerObject.transform.SetParent(owner.transform, false);
            towerObject.transform.position = position;
            TowerRuntimeView view = towerObject.AddComponent<TowerRuntimeView>();

            FieldInfo field = typeof(TowerNetworkSystem).GetField(
                "placementCombatDefinition",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(system, definition);

            MethodInfo method = typeof(TowerNetworkSystem).GetMethod(
                "HandleTowerPlaced",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(
                system,
                new object[]
                {
                    new GridPlacementCommit(
                        definition.Core.PlacementDefinition,
                        towerObject,
                        default,
                        ownerId)
                });
            return view;
        }

        private static void ConfigureSchedule(
            WaveScheduleDefinition schedule,
            EnemyDefinition enemy,
            int waveCount = 2)
        {
            var serialized = new SerializedObject(schedule);
            serialized.FindProperty("randomSeed").intValue = 1234;
            SerializedProperty waves = serialized.FindProperty("waves");
            waves.arraySize = waveCount;
            for (int index = 0; index < waves.arraySize; index++)
            {
                SerializedProperty batches = waves.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("spawnBatches");
                batches.arraySize = 1;
                SerializedProperty batch = batches.GetArrayElementAtIndex(0);
                batch.FindPropertyRelative("enemy").objectReferenceValue = enemy;
                batch.FindPropertyRelative("count").intValue = 2;
                batch.FindPropertyRelative("startTimeSeconds").floatValue = 1f;
                batch.FindPropertyRelative("spawnWindowSeconds").floatValue = 0.5f;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Fresh progress with nothing cleared, so unlock-gated towers report as locked.
        /// </summary>
        private static SaveSystem CreateSaveSystem()
        {
            var saveSystem = new SaveSystem(new SaveRepositoryStub(), "test");
            saveSystem.Initialize();
            return saveSystem;
        }

        private static TowerNetworkSystem CreateTowerNetworkSystem()
        {
            BoardDefinition board = AssetDatabase.LoadAssetAtPath<BoardDefinition>(BoardPath);
            TowerCatalog towerCatalog = AssetDatabase.LoadAssetAtPath<TowerCatalog>(TowerCatalogPath);
            Assert.That(board, Is.Not.Null);
            Assert.That(towerCatalog, Is.Not.Null);

            var boardSystem = new BoardSystem(new BoardViewStub(board));
            var inputSystem = new GameplayInputSystem(new GameplayInputSourceStub());
            var placementSystem = new GridPlacementSystem(
                boardSystem,
                inputSystem,
                new GridPlacementViewStub(),
                new TowerInstanceFactoryStub());
            return new TowerNetworkSystem(
                new TowerNetworkManager(towerCatalog),
                placementSystem,
                1,
                new LevelGoldSystem(1000));
        }

        private sealed class SaveRepositoryStub : ISaveRepository
        {
            private SaveSnapshot stored;

            public SaveLoadResult Load()
            {
                return stored == null
                    ? new SaveLoadResult(SaveLoadStatus.Missing, null, string.Empty)
                    : new SaveLoadResult(SaveLoadStatus.Success, stored, string.Empty);
            }

            public SaveWriteResult Save(SaveSnapshot snapshot)
            {
                stored = snapshot;
                return new SaveWriteResult(SaveWriteStatus.Success, string.Empty);
            }

            public SaveWriteResult DeleteOwnedAutosave()
            {
                stored = null;
                return new SaveWriteResult(SaveWriteStatus.Success, string.Empty);
            }
        }

        private sealed class GameplayViewStub : IGameplayUIView
        {
            public int ShowCount { get; private set; }

            public void Show()
            {
                ShowCount++;
            }
        }

        private sealed class LevelStatusHudViewStub : ILevelStatusHudView
        {
            public int LastGold { get; private set; }
            public int LastHealth { get; private set; }
            public int LastMaximumHealth { get; private set; }

            public void RenderGold(int gold)
            {
                LastGold = gold;
            }

            public void RenderHealth(int currentHealth, int maximumHealth)
            {
                LastHealth = currentHealth;
                LastMaximumHealth = maximumHealth;
            }

            public void SetHealthVisible(bool visible)
            {
            }
        }

#pragma warning disable CS0067 // Interface events are intentionally unused by this focused stub.
        private sealed class TowerNetworkHudViewStub : ITowerNetworkHudView
        {
            public event Action<TowerCombatDefinition, TowerPlacementPointerEvent> TowerDragBegan;
            public event Action<TowerPlacementPointerEvent> TowerDragMoved;
            public event Action<TowerPlacementPointerEvent> TowerDragEnded;
            public event Action<int> TowerDragCanceled;
            public event Action UnlinkRequested;
            public event Action SellRequested;
            public event Action UpgradeRequested;
            public event Action ReturnToMenuRequested;

            public int InitializeCount { get; private set; }
            public int RenderCount { get; private set; }
            public int ShowCount { get; private set; }
            public TowerNetworkHudState LastState { get; private set; }

            public IReadOnlyList<TowerCombatDefinition> LastLockedDefinitions { get; private set; }

            public void Initialize()
            {
                InitializeCount++;
            }

            public void ApplyTowerLocks(IReadOnlyList<TowerCombatDefinition> lockedDefinitions)
            {
                LastLockedDefinitions = lockedDefinitions;
            }

            public void SetTowerActionsAvailable(bool available)
            {
            }

            public void Render(TowerNetworkHudState state)
            {
                LastState = state;
                RenderCount++;
            }

            public void Show()
            {
                ShowCount++;
            }

            public void RequestReturnToMenu()
            {
                ReturnToMenuRequested?.Invoke();
            }
        }
#pragma warning restore CS0067

        private sealed class WaveSystemStub : IWaveSystem
        {
            public event Action StateChanged;

            public bool IsRunning => false;
            public bool IsCurrentWaveRetryAvailable => false;
            public bool HasRetriedCurrentWave => false;
            public int CurrentWaveNumber { get; set; } = 1;
            public WavePhase Phase { get; private set; } = WavePhase.Preparation;

            public WaveState CreateState()
            {
                return new WaveState(
                    Phase,
                    currentWaveNumber: CurrentWaveNumber,
                    waveCount: 1,
                    livingEnemyCount: 0,
                    canStartWave: false);
            }

            public IReadOnlyList<EnemySpawnBatchDefinition> GetNextWavePreview()
            {
                return Array.Empty<EnemySpawnBatchDefinition>();
            }

            public bool TryStartWave(out string error)
            {
                error = "Not configured.";
                return false;
            }

            public bool RetryCurrentWave() => false;

            public void ForceVictory()
            {
                Phase = WavePhase.Victory;
                StateChanged?.Invoke();
            }

            public void ForceSkipWave()
            {
                Phase = WavePhase.Preparation;
                StateChanged?.Invoke();
            }

            public void PublishStateChanged()
            {
                StateChanged?.Invoke();
            }
        }

        private sealed class WaveHudViewStub : IWaveHudView
        {
            public event Action StartWaveRequested
            {
                add { }
                remove { }
            }

            public event Action<EnemyDefinition> EnemyDescriptionOpened
            {
                add { }
                remove { }
            }

            public int RenderCount { get; private set; }
            public int ShowCount { get; private set; }

            public void Initialize()
            {
            }

            public void Render(WaveHudState state)
            {
                _ = state;
                RenderCount++;
            }

            public void Show()
            {
                ShowCount++;
            }
        }

        private sealed class BoardViewStub : IBoardView
        {
            public BoardViewStub(BoardDefinition board)
            {
                Board = board;
            }

            public BoardDefinition Board { get; }
            public Vector3 WorldOrigin => Vector3.zero;

            public void ApplyVisibility(bool visible)
            {
                _ = visible;
            }
        }

        private sealed class GameplayInputSourceStub : IGameplayInputSource
        {
            public GameplayInputSnapshot Capture()
            {
                return default;
            }
        }

        private sealed class GridPlacementViewStub : IGridPlacementView
        {
            public bool TryGetWorldPoint(
                Vector2 screenPosition,
                bool offsetForFinger,
                out Vector3 worldPoint)
            {
                _ = screenPosition;
                _ = offsetForFinger;
                worldPoint = default;
                return false;
            }

            public void Show(
                TowerFootprint footprint,
                Vector3 footprintBottomCenter,
                float cellSize,
                float heightUnit,
                float linkRangeMeters,
                bool isValid)
            {
                _ = footprint;
                _ = footprintBottomCenter;
                _ = cellSize;
                _ = heightUnit;
                _ = linkRangeMeters;
                _ = isValid;
            }

            public void Hide()
            {
            }
        }

        private sealed class TowerInstanceFactoryStub : ITowerInstanceFactory
        {
            public bool TryCreate(
                TowerDefinition definition,
                Vector3 position,
                out GameObject instance)
            {
                _ = definition;
                _ = position;
                instance = null;
                return false;
            }

            public void Destroy(GameObject instance)
            {
                _ = instance;
            }
        }
    }
}
