using System;
using System.Collections.Generic;
using TowerDefense3D.Economy;
using TowerDefense3D.Enemies;
using TowerDefense3D.Frog;
using TowerDefense3D.GameplayInput;
using TowerDefense3D.GridPlacement;
using TowerDefense3D.Simulation;
using TowerDefense3D.Towers;
using TowerDefense3D.Tutorials;
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
        [Tooltip("The reach ring drawn around a selected tower, and around the tower a link is being dragged from. Authored per level and handed to the container explicitly, because the placement preview owns a ring of its own and searching the hierarchy for the type would find whichever came first.")]
        [SerializeField] private LinkRangeRingView selectionRangeRing;

        [SerializeField, Min(1)] private int levelNumber = 1;
        [SerializeField] private WaveScheduleDefinition waveSchedule;

        [Tooltip("Where the standing boss waits, if this level has one. Drag it onto the spot; "
            + "the boss appears there, snapped onto the road. Leave empty otherwise.")]
        [SerializeField] private StandingBossAnchorView standingBossAnchor;

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
                .AsSelf()
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
            builder.Register<WaveSystem>(
                resolver => new WaveSystem(
                    waveSchedule,
                    resolver.Resolve<EnemySystem>(),
                    resolver.Resolve<TowerNetworkSystem>(),
                    resolver.Resolve<WaveSpawnPlanner>(),
                    resolver.Resolve<LevelGoldSystem>(),
                    resolver.Resolve<LevelBaseHealthSystem>(),
                    // Optional on purpose: a level without a standing boss has no marker, and
                    // asking the container for one that is not there would fail the whole scope.
                    standingBossAnchor),
                Lifetime.Scoped)
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
            builder.RegisterInstance<ILinkRangeView>(selectionRangeRing);
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
            try
            {
                Container?.Resolve<TutorialSystem>()?.UnbindLevel();
            }
            catch
            {
                // The parent application scope may already be tearing down.
            }

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
            container.Resolve<FrogVictoryEscapeView>()
                .BindHealth(container.Resolve<LevelBaseHealthSystem>());
            AdoptAuthoredTowers(container.Resolve<TowerNetworkSystem>());
            BindTutorialContext(
                container.Resolve<TutorialSystem>(),
                container.Resolve<TowerNetworkSystem>(),
                container.Resolve<IWaveSystem>(),
                container.Resolve<GameplayInputSystem>());
            activeLevelSystems.Attach(systems);
            attachedSystems = systems;
        }

        private void BindTutorialContext(
            TutorialSystem tutorialSystem,
            TowerNetworkSystem towerNetwork,
            IWaveSystem waveSystem,
            GameplayInputSystem inputSystem)
        {
            GameplayUIView gameplayView = FindSceneComponent<GameplayUIView>();
            TutorialGameplayUiStageView tutorialUiStage = gameplayView.GetComponent<TutorialGameplayUiStageView>()
                ?? gameplayView.gameObject.AddComponent<TutorialGameplayUiStageView>();
            tutorialUiStage.Initialize();
            TutorialTargetView[] targets = FindObjectsByType<TutorialTargetView>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            var byId = new Dictionary<string, TutorialTargetView>(StringComparer.Ordinal);
            for (int index = 0; index < targets.Length; index++)
            {
                TutorialTargetView target = targets[index];
                if (!string.IsNullOrEmpty(target.TargetId))
                {
                    byId[target.TargetId] = target;
                }
            }

            WaveHudView waveHud = gameplayView.GetComponentInChildren<WaveHudView>(true);
            if (waveHud?.NextWaveToggleTransform != null)
            {
                TutorialTargetView nextWaveTarget = waveHud.NextWaveToggleTransform
                    .GetComponent<TutorialTargetView>()
                    ?? waveHud.NextWaveToggleTransform.gameObject.AddComponent<TutorialTargetView>();
                nextWaveTarget.SetTargetId("next_wave");
                byId["next_wave"] = nextWaveTarget;
            }

            FrogVictoryEscapeView sceneFrog = FindSceneComponent<FrogVictoryEscapeView>();
            if (sceneFrog != null)
            {
                TutorialTargetView frogTarget = sceneFrog.GetComponent<TutorialTargetView>()
                    ?? sceneFrog.gameObject.AddComponent<TutorialTargetView>();
                frogTarget.SetTargetId("frog");
                byId["frog"] = frogTarget;
            }

            AuthoredTowerView[] authoredTowers = FindObjectsByType<AuthoredTowerView>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int index = 0; index < authoredTowers.Length; index++)
            {
                AuthoredTowerView tower = authoredTowers[index];
                if (tower.gameObject.scene != gameObject.scene || tower.Definition == null)
                {
                    continue;
                }

                string id = tower.Definition.Family == TowerFamily.Generator
                    ? "generator"
                    : tower.Definition.Family == TowerFamily.SoulNexus
                        ? "soul_nexus"
                        : string.Empty;
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                TutorialTargetView target = tower.GetComponent<TutorialTargetView>()
                    ?? tower.gameObject.AddComponent<TutorialTargetView>();
                target.SetTargetId(id);
                byId[id] = target;
            }

            tutorialSystem.BindLevel(new TutorialContext(
                levelNumber,
                id => byId.ContainsKey(id),
                id => byId.TryGetValue(id, out TutorialTargetView target) ? target.transform : null,
                id => id == "tutorial_acknowledged"
                    ? inputSystem.Current.HasPointerInput && inputSystem.Current.WasPressed
                    : id == "wave_running"
                    ? waveSystem.IsRunning
                    : id == "generator_selected"
                        ? towerNetwork.SelectedTower?.CombatDefinition?.Family == TowerFamily.Generator
                        : id == "generator_linked" && towerNetwork.HasValidChain,
                tutorialUiStage.SetMode,
                () => inputSystem.Current.HasPointerInput && inputSystem.Current.WasPressed));
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

                    GridCell? authoredAnchor = authoredTower.HasBoardAnchor
                        ? authoredTower.BoardAnchor
                        : (GridCell?)null;
                    if (!towerNetworkSystem.TryRegisterAuthoredTower(
                            authoredTower.RuntimeView,
                            authoredTower.Definition,
                            authoredAnchor,
                            out string error))
                    {
                        Debug.LogWarning($"'{authoredTower.name}' was not adopted: {error}", authoredTower);
                    }
                }
            }
        }

        private void BindLevelOutcomeHud(IObjectResolver container, GameFlowSystem gameFlowSystem)
        {
            bool hasNextLevel = container.Resolve<LevelCatalog>()
                .TryGetNextLevel(levelNumber, out _);
            int currentLevelNumber = levelNumber;
            container.Resolve<LevelOutcomeHudPresenter>().BindLevel(
                hasNextLevel,
                () => gameFlowSystem.RequestReplayLevel(currentLevelNumber),
                () => gameFlowSystem.RequestPlayNextLevel(currentLevelNumber),
                gameFlowSystem.RequestReturnToLevelMenu,
                stars => gameFlowSystem.ReportLevelCleared(currentLevelNumber, stars));
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
