using System;
using System.Collections.Generic;
using NUnit.Framework;
using TowerDefense3D.Economy;
using TowerDefense3D.Enemies;
using TowerDefense3D.GameplayInput;
using TowerDefense3D.GridPlacement;
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
            var placementHudView = new PlacementHudViewStub();
            var towerHudView = new TowerNetworkHudViewStub();
            var presenter = new TowerNetworkHudPresenter(towerNetworkSystem, towerHudView);
            var waveSystem = new WaveSystemStub();
            var waveHudView = new WaveHudViewStub();
            var wavePresenter = new WaveHudPresenter(waveSystem, waveHudView);
            var statusHudView = new LevelStatusHudViewStub();
            var gameplayUISystem = new GameplayUISystem(
                gameplayView,
                placementHudView,
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
            Assert.That(placementHudView.ShowCount, Is.EqualTo(1));
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

        private sealed class GameplayViewStub : IGameplayUIView
        {
            public int ShowCount { get; private set; }

            public void Show()
            {
                ShowCount++;
            }
        }

        private sealed class PlacementHudViewStub : IPlacementHudView
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
        }

#pragma warning disable CS0067 // Interface events are intentionally unused by this focused stub.
        private sealed class TowerNetworkHudViewStub : ITowerNetworkHudView
        {
            public event Action<TowerCombatDefinition, TowerPlacementPointerEvent> TowerDragBegan;
            public event Action<TowerPlacementPointerEvent> TowerDragMoved;
            public event Action<TowerPlacementPointerEvent> TowerDragEnded;
            public event Action<int> TowerDragCanceled;
            public event Action UnlinkRequested;
            public event Action CancelPlacementRequested;
            public event Action ReturnToMenuRequested;

            public int InitializeCount { get; private set; }
            public int RenderCount { get; private set; }
            public int ShowCount { get; private set; }
            public TowerNetworkHudState LastState { get; private set; }

            public void Initialize()
            {
                InitializeCount++;
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

            public WaveState CreateState()
            {
                return new WaveState(
                    WavePhase.Preparation,
                    currentWaveNumber: 1,
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
            public bool TryGetWorldPoint(Vector2 screenPosition, out Vector3 worldPoint)
            {
                _ = screenPosition;
                worldPoint = default;
                return false;
            }

            public void Show(
                TowerFootprint footprint,
                Vector3 footprintBottomCenter,
                float cellSize,
                float heightUnit,
                bool isValid)
            {
                _ = footprint;
                _ = footprintBottomCenter;
                _ = cellSize;
                _ = heightUnit;
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
