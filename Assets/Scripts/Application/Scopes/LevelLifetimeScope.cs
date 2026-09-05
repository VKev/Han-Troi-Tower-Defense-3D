using System;
using TowerDefense3D.Economy;
using TowerDefense3D.Enemies;
using TowerDefense3D.Frog;
using TowerDefense3D.GameplayInput;
using TowerDefense3D.GridPlacement;
using TowerDefense3D.Simulation;
using TowerDefense3D.Towers;
using TowerDefense3D.Waves;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Composes one additive level and attaches its explicit system group to the application entry point.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelLifetimeScope : LifetimeScope
    {
        [SerializeField, Min(1)] private int levelNumber = 1;
        [SerializeField] private WaveScheduleDefinition waveSchedule;

        private ActiveLevelSystemSlot activeLevelSystems;
        private LevelSystemGroup attachedSystems;
        private GridPlacementPresenter placementPresenter;

        public int LevelNumber => levelNumber;

        protected override void Configure(IContainerBuilder builder)
        {
            if (levelNumber <= 0)
            {
                throw new InvalidOperationException("LevelLifetimeScope requires a positive authored level number.");
            }

            if (waveSchedule == null)
            {
                throw new InvalidOperationException("LevelLifetimeScope requires an authored Wave Schedule.");
            }

            GridPlacementView placementView = FindSceneComponent<GridPlacementView>();
            if (placementView.WorldCamera == null)
            {
                throw new InvalidOperationException("GridPlacementView requires an authored world camera.");
            }

            builder.RegisterComponentInHierarchy<BoardView>()
                .As<IBoardView>();
            builder.RegisterComponentInHierarchy<BoardCameraView>()
                .As<IBoardCameraView>();
            builder.RegisterComponentInHierarchy<GameplayInputSource>()
                .As<IGameplayInputSource>();
            builder.RegisterComponentInHierarchy<GridPlacementView>()
                .AsSelf()
                .As<IGridPlacementView>();
            builder.RegisterComponentInHierarchy<TowerInstanceFactory>()
                .As<ITowerInstanceFactory>();
            builder.RegisterComponentInHierarchy<TowerLinkView>()
                .As<ITowerLinkView>();
            builder.RegisterComponentInHierarchy<TowerProjectilePoolView>()
                .As<ITowerProjectileViewPool>();
            builder.RegisterComponentInHierarchy<GameplayUIView>()
                .As<IGameplayUIView>();
            builder.RegisterComponentInHierarchy<LevelStatusHudView>()
                .As<ILevelStatusHudView>();
            builder.RegisterComponentInHierarchy<PauseHudView>()
                .As<IPauseHudView>();
            builder.RegisterComponentInHierarchy<LevelSkipCheatView>()
                .As<ILevelSkipCheatView>();
            builder.RegisterComponentInHierarchy<LevelOutcomeHudView>()
                .As<ILevelOutcomeHudView>();
            builder.RegisterComponentInHierarchy<PauseMenuHudView>()
                .As<IPauseMenuHudView>();
            builder.RegisterComponentInHierarchy<TowerNetworkHudView>()
                .As<ITowerNetworkHudView>();
            builder.RegisterComponentInHierarchy<WaveHudView>()
                .As<IWaveHudView>();
            builder.RegisterComponentInHierarchy<FrogVictoryEscapeView>()
                .As<ILevelVictoryEscapeView>();
            builder.RegisterComponentInHierarchy<EnemyViewPool>()
                .AsSelf()
                .As<IEnemyViewPool>();
            builder.RegisterComponentInHierarchy<SafeAreaView>()
                .As<ISafeAreaView>();
            builder.Register<SafeAreaSystem>(Lifetime.Scoped);
            builder.RegisterComponentInHierarchy<GridPlacementPresenter>();
            builder.RegisterInstance(placementView.WorldCamera);
            builder.RegisterInstance(waveSchedule);
            builder.Register<LevelGoldSystem>(
                resolver => new LevelGoldSystem(GetLevelEntry(resolver).StartingGold),
                Lifetime.Scoped);
            builder.Register<LevelBaseHealthSystem>(
                resolver => new LevelBaseHealthSystem(GetLevelEntry(resolver).StartingHealth),
                Lifetime.Scoped);
            builder.Register<BoardSystem>(Lifetime.Scoped);
            builder.Register<RoadPathSet>(
                resolver => RoadPathFactory.CreatePaths(resolver.Resolve<BoardSystem>()),
                Lifetime.Scoped);
            builder.Register<BoardCameraSystem>(Lifetime.Scoped);
            builder.Register<GameplayInputSystem>(Lifetime.Scoped);
            builder.Register<GridPlacementSystem>(Lifetime.Scoped);
            builder.Register<TowerNetworkSystem>(Lifetime.Scoped)
                .WithParameter("levelNumber", levelNumber);
            builder.Register<TowerInteractionSystem>(Lifetime.Scoped);
            builder.Register<EnemySystem>(
                resolver => new EnemySystem(
                    resolver.Resolve<RoadPathSet>(),
                    resolver.Resolve<LevelGoldSystem>(),
                    resolver.Resolve<LevelBaseHealthSystem>()),
                Lifetime.Scoped);
            builder.Register<WaveSpawnPlanner>(Lifetime.Scoped);
            builder.Register<WaveSystem>(Lifetime.Scoped)
                .AsSelf()
                .As<IWaveSystem>();
            builder.Register<CombatTimelinePlanner>(
                resolver => new CombatTimelinePlanner(
                    resolver.Resolve<TowerNetworkManager>(),
                    resolver.Resolve<RoadPathSet>(),
                    resolver.Resolve<ElementReactionCatalog>()),
                Lifetime.Scoped);
            builder.Register<CombatTimelineSystem>(Lifetime.Scoped);
            builder.Register<GameplaySimulationSystem>(Lifetime.Scoped);
            builder.Register<EnemyPresentationSystem>(Lifetime.Scoped);
            builder.Register<HeroAttackPresentationSystem>(Lifetime.Scoped);
            builder.Register<TowerLinkPresentationSystem>(Lifetime.Scoped);
            builder.Register<TowerProjectilePresentationSystem>(Lifetime.Scoped);
            builder.Register<TowerNetworkHudPresenter>(Lifetime.Scoped);
            builder.Register<WaveHudPresenter>(Lifetime.Scoped);
            builder.Register<LevelSkipCheatPresenter>(Lifetime.Scoped);
            builder.Register<LevelOutcomeHudPresenter>(Lifetime.Scoped);
            builder.Register<PauseMenuHudPresenter>(Lifetime.Scoped);
            builder.Register<GameplayUISystem>(Lifetime.Scoped);
            builder.Register<LevelSystemGroup>(Lifetime.Scoped);
            builder.RegisterBuildCallback(AttachLevelSystems);
        }

        protected override void OnDestroy()
        {
            ReleaseLevelSystems();
            base.OnDestroy();
        }

        internal void ReleaseLevelSystems()
        {
            if (attachedSystems != null)
            {
                activeLevelSystems.DetachForScopeTeardown(attachedSystems);
                attachedSystems = null;
            }

            if (placementPresenter != null)
            {
                placementPresenter.Shutdown();
                placementPresenter = null;
            }

            DisposeCore();
        }

        private void AttachLevelSystems(IObjectResolver container)
        {
            activeLevelSystems = container.Resolve<ActiveLevelSystemSlot>();
            placementPresenter = container.Resolve<GridPlacementPresenter>();
            GridPlacementSystem placementSystem = container.Resolve<GridPlacementSystem>();
            GridPlacementView placementView = container.Resolve<GridPlacementView>();
            placementPresenter.Bind(placementSystem, placementView);
            container.Resolve<EnemyViewPool>().Configure(placementView.WorldCamera);
            GameFlowSystem gameFlowSystem = container.Resolve<GameFlowSystem>();
            container.Resolve<GameplayUISystem>()
                .BindReturnToMenu(gameFlowSystem.RequestReturnToLevelMenu);
            BindLevelOutcomeHud(container, gameFlowSystem);
            BindPauseMenuHud(container, gameFlowSystem);

            LevelSystemGroup systems = container.Resolve<LevelSystemGroup>();
            systems.Start();
            AdoptAuthoredTowers(container.Resolve<TowerNetworkSystem>());
            activeLevelSystems.Attach(systems);
            attachedSystems = systems;
        }

        /// <summary>
        /// Hands every tower the scene authored to the tower network, after its systems have
        /// started and opened a level session. One that the board rejects is reported and left
        /// as scenery rather than failing the whole level load.
        /// </summary>
        private void AdoptAuthoredTowers(TowerNetworkSystem towerNetworkSystem)
        {
            GameObject[] roots = gameObject.scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                AuthoredTowerView[] authoredTowers =
                    roots[rootIndex].GetComponentsInChildren<AuthoredTowerView>(true);
                for (int index = 0; index < authoredTowers.Length; index++)
                {
                    AuthoredTowerView authoredTower = authoredTowers[index];
                    if (authoredTower.Definition == null)
                    {
                        Debug.LogError(
                            $"'{authoredTower.name}' is an authored tower without a combat definition.",
                            authoredTower);
                        continue;
                    }

                    if (!towerNetworkSystem.TryRegisterAuthoredTower(
                            authoredTower.RuntimeView,
                            authoredTower.Definition,
                            out string error))
                    {
                        Debug.LogWarning($"'{authoredTower.name}' was not adopted: {error}", authoredTower);
                    }
                }
            }
        }

        private void BindLevelOutcomeHud(IObjectResolver container, GameFlowSystem gameFlowSystem)
        {
            LevelCatalogEntry entry = GetLevelEntry(container);
            bool hasNextLevel = container.Resolve<LevelCatalog>()
                .TryGetNextLevel(levelNumber, out _);
            int currentLevelNumber = levelNumber;
            container.Resolve<LevelOutcomeHudPresenter>().BindLevel(
                entry.DisplayName,
                hasNextLevel,
                () => gameFlowSystem.RequestReplayLevel(currentLevelNumber),
                () => gameFlowSystem.RequestPlayNextLevel(currentLevelNumber),
                gameFlowSystem.RequestReturnToLevelMenu,
                () => gameFlowSystem.ReportLevelCleared(currentLevelNumber));
        }

        /// <summary>
        /// Binds the pause modal's two navigation commands. Resume is not one of them: it stays
        /// inside the HUD, where the pause button and the modal move together.
        /// </summary>
        private void BindPauseMenuHud(IObjectResolver container, GameFlowSystem gameFlowSystem)
        {
            int currentLevelNumber = levelNumber;
            container.Resolve<PauseMenuHudPresenter>().BindLevel(
                () => gameFlowSystem.RequestReplayLevel(currentLevelNumber),
                gameFlowSystem.RequestReturnToLevelMenu);
        }

        private T FindSceneComponent<T>() where T : Component
        {
            GameObject[] roots = gameObject.scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                T component = roots[index].GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            throw new InvalidOperationException($"Level scene requires {typeof(T).Name}.");
        }

        private LevelCatalogEntry GetLevelEntry(IObjectResolver resolver)
        {
            LevelCatalog catalog = resolver.Resolve<LevelCatalog>();
            if (catalog.TryGetLevel(levelNumber, out LevelCatalogEntry entry))
            {
                return entry;
            }

            throw new InvalidOperationException(
                $"LevelLifetimeScope requires a LevelCatalog entry for Level {levelNumber}.");
        }
    }
}
