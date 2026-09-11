using System;
using System.Collections.Generic;
using TowerDefense3D.Audio;
using TowerDefense3D.Economy;
using TowerDefense3D.Enemies;
using TowerDefense3D.Frog;
using TowerDefense3D.GameplayInput;
using TowerDefense3D.GridPlacement;
using TowerDefense3D.Mobile;
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
        private GridPlacementSystem tutorialPlacementSystem;
        private Action<GridPlacementCommit> tutorialPlacementHandler;

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
                .As<ILevelStatusHudView>()
                .As<IWaveThreeDefeatHudView>();
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
            builder.Register<BoardCameraGestureSystem>(Lifetime.Scoped);
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
                    standingBossAnchor,
                    instantDefeatWaveNumber: levelNumber == 1 || levelNumber == 2 ? 3 : 0),
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
            builder.Register<SoundCueSystem>(Lifetime.Scoped);
            builder.Register<LevelPreparationMusicSystem>(Lifetime.Scoped);
            builder.Register<GameplaySimulationSystem>(Lifetime.Scoped);
            builder.Register<TutorialFocusSystem>(Lifetime.Scoped);
            builder.Register<EnemyPresentationSystem>(Lifetime.Scoped);
            builder.Register<HeroAttackPresentationSystem>(Lifetime.Scoped);
            builder.Register<TowerLinkPresentationSystem>(Lifetime.Scoped);
            builder.Register<TowerProjectilePresentationSystem>(Lifetime.Scoped);
            builder.Register<TowerTierVisualPresentationSystem>(Lifetime.Scoped);
            builder.RegisterInstance<ILinkRangeView>(selectionRangeRing);
            builder.Register(
                resolver => new TowerNetworkHudPresenter(
                    resolver.Resolve<TowerNetworkSystem>(),
                    resolver.Resolve<ITowerNetworkHudView>(),
                    resolver.Resolve<Camera>(),
                    resolver.Resolve<TowerCatalog>(),
                    resolver.Resolve<SaveSystem>(),
                    resolver.Resolve<TutorialProgress>(),
                    levelNumber,
                    resolver.Resolve<IWaveSystem>(),
                    resolver.Resolve<LevelGoldSystem>()),
                Lifetime.Scoped);
            builder.Register(
                resolver => new WaveHudPresenter(
                    resolver.Resolve<IWaveSystem>(),
                    resolver.Resolve<IWaveHudView>(),
                    resolver.Resolve<EnemyDiscoveryProgress>()),
                Lifetime.Scoped);
            builder.Register<LevelSkipCheatPresenter>(Lifetime.Scoped);
            builder.Register<LevelOutcomeHudPresenter>(Lifetime.Scoped);
            builder.Register<WaveThreeDefeatHudPresenter>(Lifetime.Scoped);
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
            if (tutorialPlacementSystem != null && tutorialPlacementHandler != null)
            {
                tutorialPlacementSystem.TowerPlaced -= tutorialPlacementHandler;
                tutorialPlacementSystem = null;
                tutorialPlacementHandler = null;
            }

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
            container.Resolve<EnemyViewPool>().Configure(
                placementView.WorldCamera,
                container.Resolve<ISoundPlayer>());
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
                placementSystem,
                container.Resolve<IWaveSystem>(),
                container.Resolve<GameplayInputSystem>(),
                container.Resolve<EnemyViewPool>(),
                container.Resolve<TutorialFocusSystem>());
            LogToonShaderDiagnostics();
            activeLevelSystems.Attach(systems);
            attachedSystems = systems;
        }

        private void LogToonShaderDiagnostics()
        {
#if UNITY_ANDROID
            const string shaderName = "TheVayuputra/ToonShader";
            Shader shader = Shader.Find(shaderName);
            string onScreenDiagnostic = "TOON: "
                + (shader != null && shader.isSupported ? "OK" : "UNSUPPORTED")
                + "\n" + SystemInfo.graphicsDeviceType + " | " + SystemInfo.graphicsDeviceName;
            FindFirstObjectByType<FpsCounterView>(FindObjectsInactive.Include)
                ?.SetDiagnostic(onScreenDiagnostic);
            Debug.Log(
                "[ToonShader] device=" + SystemInfo.deviceModel
                + "; gpu=" + SystemInfo.graphicsDeviceName
                + "; api=" + SystemInfo.graphicsDeviceType
                + "; version=" + SystemInfo.graphicsDeviceVersion
                + "; shaderFound=" + (shader != null)
                + "; shaderSupported=" + (shader != null && shader.isSupported));

            var loggedMaterials = new HashSet<Material>();
            Renderer[] renderers = FindObjectsByType<Renderer>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null || renderer.gameObject.scene != gameObject.scene)
                {
                    continue;
                }

                Material[] materials = renderer.sharedMaterials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material material = materials[materialIndex];
                    if (material == null
                        || material.shader == null
                        || material.shader.name != shaderName
                        || !loggedMaterials.Add(material))
                    {
                        continue;
                    }

                    Debug.Log(
                        "[ToonShader] material=" + material.name
                        + "; renderer=" + renderer.name
                        + "; supported=" + material.shader.isSupported
                        + "; passCount=" + material.passCount
                        + "; keywords=" + string.Join(",", material.shaderKeywords));
                }
            }
#endif
        }

        private void BindTutorialContext(
            TutorialSystem tutorialSystem,
            TowerNetworkSystem towerNetwork,
            GridPlacementSystem placementSystem,
            IWaveSystem waveSystem,
            GameplayInputSystem inputSystem,
            EnemyViewPool enemyViewPool,
            TutorialFocusSystem focusSystem)
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
            LevelStatusHudView levelStatus = gameplayView.GetComponentInChildren<LevelStatusHudView>(true);
            if (levelStatus != null)
            {
                TutorialTargetView frogHudTarget = levelStatus.GetComponent<TutorialTargetView>()
                    ?? levelStatus.gameObject.AddComponent<TutorialTargetView>();
                frogHudTarget.SetTargetId("frog_hud");
                byId["frog_hud"] = frogHudTarget;
            }

            if (waveHud?.NextWaveToggleTransform != null)
            {
                TutorialTargetView nextWaveTarget = waveHud.NextWaveToggleTransform
                    .GetComponent<TutorialTargetView>()
                    ?? waveHud.NextWaveToggleTransform.gameObject.AddComponent<TutorialTargetView>();
                nextWaveTarget.SetTargetId("next_wave");
                byId["next_wave"] = nextWaveTarget;
            }

            TowerNetworkHudView towerHud = gameplayView.GetComponentInChildren<TowerNetworkHudView>(true);
            if (towerHud != null)
            {
                Transform tutorialHudTargetTransform = towerHud.GetTutorialHudTargetTransform();
                TutorialTargetView towerHudTarget = tutorialHudTargetTransform.GetComponent<TutorialTargetView>()
                    ?? tutorialHudTargetTransform.gameObject.AddComponent<TutorialTargetView>();
                towerHudTarget.SetTargetId("tower_hud");
                byId["tower_hud"] = towerHudTarget;

                Transform generatorCard = towerHud.GetTowerButtonTransform(TowerFamily.Generator);
                if (generatorCard != null)
                {
                    TutorialTargetView generatorCardTarget = generatorCard.GetComponent<TutorialTargetView>()
                        ?? generatorCard.gameObject.AddComponent<TutorialTargetView>();
                    generatorCardTarget.SetTargetId("generator_card");
                    byId["generator_card"] = generatorCardTarget;
                }

                Transform sinkCard = towerHud.GetTowerButtonTransform(TowerFamily.SoulNexus);
                if (sinkCard != null)
                {
                    TutorialTargetView sinkCardTarget = sinkCard.GetComponent<TutorialTargetView>()
                        ?? sinkCard.gameObject.AddComponent<TutorialTargetView>();
                    sinkCardTarget.SetTargetId("sink_card");
                    byId["sink_card"] = sinkCardTarget;
                }

                Transform fireCard = towerHud.GetTowerDragButtonTransform(TowerFamily.Fire);
                if (fireCard != null)
                {
                    TutorialTargetView fireCardTarget = fireCard.GetComponent<TutorialTargetView>()
                        ?? fireCard.gameObject.AddComponent<TutorialTargetView>();
                    fireCardTarget.SetTargetId("fire_card");
                    byId["fire_card"] = fireCardTarget;
                }

                Transform waterCard = towerHud.GetTowerDragButtonTransform(TowerFamily.Water);
                if (waterCard != null)
                {
                    TutorialTargetView waterCardTarget = waterCard.GetComponent<TutorialTargetView>()
                        ?? waterCard.gameObject.AddComponent<TutorialTargetView>();
                    waterCardTarget.SetTargetId("water_card");
                    byId["water_card"] = waterCardTarget;
                }

                RegisterTutorialTarget(
                    towerHud.GetUnlinkButtonTransform(),
                    "unlink_button",
                    byId);
                RegisterTutorialTarget(
                    towerHud.GetUpgradeButtonTransform(),
                    "upgrade_button",
                    byId);
            }

            if (waveHud?.NextEnemyTransform != null)
            {
                TutorialTargetView nextEnemyTarget = waveHud.NextEnemyTransform
                    .GetComponent<TutorialTargetView>()
                    ?? waveHud.NextEnemyTransform.gameObject.AddComponent<TutorialTargetView>();
                nextEnemyTarget.SetTargetId("next_enemy");
                byId["next_enemy"] = nextEnemyTarget;
            }

            if (waveHud?.NextEnemyDescriptionTransform != null)
            {
                TutorialTargetView enemyDescriptionTarget = waveHud.NextEnemyDescriptionTransform
                    .GetComponent<TutorialTargetView>()
                    ?? waveHud.NextEnemyDescriptionTransform.gameObject.AddComponent<TutorialTargetView>();
                enemyDescriptionTarget.SetTargetId("next_enemy_description");
                byId["next_enemy_description"] = enemyDescriptionTarget;
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
                        : levelNumber == 2 && tower.Definition.Family == TowerFamily.Fire
                            ? "level_two_fire"
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

            BoardView boardView = FindSceneComponent<BoardView>();
            Transform placementTarget = CreatePlacementTarget(
                boardView, new GridCell(35, 33, 0), "Generator Tutorial Placement Target",
                out GridCell placementCell);
            RegisterTutorialTarget(placementTarget, "generator_placement", byId);
            Transform sinkPlacementTarget = CreatePlacementTarget(
                boardView, new GridCell(32, 33, 0), "Sink Tutorial Placement Target",
                out GridCell sinkPlacementCell);
            RegisterTutorialTarget(sinkPlacementTarget, "sink_placement", byId);
            Transform secondGeneratorPlacementTarget = CreatePlacementTarget(
                boardView, new GridCell(37, 32, 0), "Second Generator Tutorial Placement Target",
                out GridCell secondGeneratorPlacementCell);
            RegisterTutorialTarget(secondGeneratorPlacementTarget, "second_generator_placement", byId);
            Transform firePlacementTarget = CreatePlacementTarget(
                boardView, new GridCell(37, 30, 0), "Fire Tutorial Placement Target",
                out GridCell firePlacementCell);
            RegisterTutorialTarget(firePlacementTarget, "fire_placement", byId);

            Transform levelTwoWaterPlacementTarget = null;
            Transform levelTwoFirePlacementTarget = null;
            Transform levelTwoSinkPlacementTarget = null;
            Transform levelTwoGeneratorPlacementTarget = null;
            GridCell levelTwoWaterPlacementCell = default;
            GridCell levelTwoFirePlacementCell = default;
            GridCell levelTwoSinkPlacementCell = default;
            GridCell levelTwoGeneratorPlacementCell = default;
            if (levelNumber == 2)
            {
                levelTwoWaterPlacementTarget = CreatePlacementTarget(
                    boardView, new GridCell(38, 27, 0), "Level Two Water Tutorial Placement Target",
                    out levelTwoWaterPlacementCell);
                RegisterTutorialTarget(
                    levelTwoWaterPlacementTarget,
                    "level_two_water_placement",
                    byId);
                levelTwoFirePlacementTarget = CreatePlacementTarget(
                    boardView, new GridCell(35, 29, 0), "Level Two Fire Tutorial Placement Target",
                    out levelTwoFirePlacementCell);
                RegisterTutorialTarget(
                    levelTwoFirePlacementTarget,
                    "level_two_fire_placement",
                    byId);
                levelTwoSinkPlacementTarget = CreatePlacementTarget(
                    boardView, new GridCell(38, 24, 0), "Level Two Sink Tutorial Placement Target",
                    out levelTwoSinkPlacementCell);
                RegisterTutorialTarget(
                    levelTwoSinkPlacementTarget,
                    "level_two_sink_placement",
                    byId);
                levelTwoGeneratorPlacementTarget = CreatePlacementTarget(
                    boardView, new GridCell(38, 34, 0), "Level Two Generator Tutorial Placement Target",
                    out levelTwoGeneratorPlacementCell);
                RegisterTutorialTarget(
                    levelTwoGeneratorPlacementTarget,
                    "level_two_generator_placement",
                    byId);
            }

            ITowerRuntimeView levelTwoTutorialWater = null;
            ITowerRuntimeView levelTwoTutorialFire = null;
            ITowerRuntimeView levelTwoTutorialSink = null;
            ITowerRuntimeView levelTwoTutorialGenerator = null;
            if (levelNumber == 2)
            {
                tutorialPlacementSystem = placementSystem;
                tutorialPlacementHandler = placement =>
                {
                    ITowerRuntimeView tower = placement.Instance != null
                        ? placement.Instance.GetComponent<TowerRuntimeView>()
                        : null;
                    if (tower?.CombatDefinition == null)
                    {
                        return;
                    }

                    if (placement.Anchor == levelTwoWaterPlacementCell
                        && tower.CombatDefinition.Family == TowerFamily.Water)
                    {
                        levelTwoTutorialWater = tower;
                    }
                    else if (placement.Anchor == levelTwoFirePlacementCell
                        && tower.CombatDefinition.Family == TowerFamily.Fire)
                    {
                        levelTwoTutorialFire = tower;
                    }
                    else if (placement.Anchor == levelTwoSinkPlacementCell
                        && tower.CombatDefinition.Family == TowerFamily.SoulNexus)
                    {
                        levelTwoTutorialSink = tower;
                    }
                    else if (placement.Anchor == levelTwoGeneratorPlacementCell
                        && tower.CombatDefinition.Family == TowerFamily.Generator)
                    {
                        levelTwoTutorialGenerator = tower;
                    }
                };
                placementSystem.TowerPlaced += tutorialPlacementHandler;
            }

            int authoredTowerCount = towerNetwork.RegisteredTowerCount;
            var initialTowerViews = new HashSet<ITowerRuntimeView>(
                towerNetwork.CreateTowerViewSnapshot());
            Func<TowerFamily, int, TutorialTargetView> findPlacedTower = (family, ordinal) =>
            {
                IReadOnlyList<ITowerRuntimeView> towers = towerNetwork.CreateTowerViewSnapshot();
                int found = 0;
                for (int index = 0; index < towers.Count; index++)
                {
                    ITowerRuntimeView tower = towers[index];
                    if (tower == null
                        || initialTowerViews.Contains(tower)
                        || tower.CombatDefinition?.Family != family)
                    {
                        continue;
                    }

                    if (found++ != ordinal)
                    {
                        continue;
                    }

                    TutorialTargetView target = tower.GameObject.GetComponent<TutorialTargetView>()
                        ?? tower.GameObject.AddComponent<TutorialTargetView>();
                    return target;
                }

                return null;
            };
            Func<TutorialTargetView> findPlacedGenerator = () => findPlacedTower(TowerFamily.Generator, 0);
            Func<TutorialTargetView> findPlacedSink = () => findPlacedTower(TowerFamily.SoulNexus, 0);
            Func<TutorialTargetView> findSecondGenerator = () => findPlacedTower(TowerFamily.Generator, 1);
            Func<TutorialTargetView> findPlacedFire = () => findPlacedTower(TowerFamily.Fire, 0);
            Func<ITowerRuntimeView, TutorialTargetView> getTutorialTarget = tower => tower == null
                ? null
                : tower.GameObject.GetComponent<TutorialTargetView>()
                    ?? tower.GameObject.AddComponent<TutorialTargetView>();
            Func<TutorialTargetView> findLevelTwoPlacedWater = () =>
                getTutorialTarget(levelTwoTutorialWater);
            Func<TutorialTargetView> findLevelTwoPlacedFire = () =>
                getTutorialTarget(levelTwoTutorialFire);
            Func<TutorialTargetView> findLevelTwoPlacedSink = () =>
                getTutorialTarget(levelTwoTutorialSink);
            Func<TutorialTargetView> findLevelTwoPlacedGenerator = () =>
                getTutorialTarget(levelTwoTutorialGenerator);
            Func<ITowerRuntimeView> findLevelTwoFire = () => byId.TryGetValue(
                "level_two_fire",
                out TutorialTargetView fireTarget)
                ? fireTarget.GetComponent(typeof(ITowerRuntimeView)) as ITowerRuntimeView
                : null;
            Func<bool> isNextEnemyDescriptionVisible = () =>
                waveHud != null && waveHud.IsNextEnemyDescriptionVisible;

            focusSystem.SetFirstFireHitCaptureEnabled(
                levelNumber == 1 && !tutorialSystem.Progress.HasCompletedLevelOneTutorial);
            Func<long> findFirstFireHitEnemyId = () => focusSystem.FirstFireHitEnemyId;
            Func<Transform> findBurningEnemyIcon = () =>
            {
                long enemyId = findFirstFireHitEnemyId();
                return enemyId != 0L
                    && enemyViewPool.TryGetActiveView(enemyId, out EnemyView markedView)
                    ? markedView.GetFireMarkIconTransform()
                    : null;
            };
            Func<TutorialTargetView> findBurningEnemyIconTarget = () =>
            {
                Transform icon = findBurningEnemyIcon();
                return icon == null
                    ? null
                    : icon.GetComponent<TutorialTargetView>()
                        ?? icon.gameObject.AddComponent<TutorialTargetView>();
            };

            tutorialSystem.BindLevel(new TutorialContext(
                levelNumber,
                id => GetRuntimeTutorialTarget(
                    id,
                    findPlacedGenerator,
                    findPlacedSink,
                    findSecondGenerator,
                    findPlacedFire,
                    findLevelTwoPlacedGenerator,
                    findLevelTwoPlacedWater,
                    findLevelTwoPlacedFire,
                    findLevelTwoPlacedSink,
                    findBurningEnemyIconTarget,
                    byId) != null,
                id => GetRuntimeTutorialTarget(
                    id,
                    findPlacedGenerator,
                    findPlacedSink,
                    findSecondGenerator,
                    findPlacedFire,
                    findLevelTwoPlacedGenerator,
                    findLevelTwoPlacedWater,
                    findLevelTwoPlacedFire,
                    findLevelTwoPlacedSink,
                    findBurningEnemyIconTarget,
                    byId)?.transform,
                id => id == "tutorial_acknowledged"
                    ? inputSystem.Current.HasPointerInput && inputSystem.Current.WasPressed
                    : id == "wave_running"
                    ? waveSystem.IsRunning
                    : id == "wave_one_ready"
                    ? waveSystem.CreateState().Phase == WavePhase.Preparation
                        && waveSystem.CreateState().CurrentWaveNumber == 1
                    : id == "wave_two_ready"
                        ? waveSystem.CreateState().Phase == WavePhase.Preparation
                            && waveSystem.CreateState().CurrentWaveNumber == 2
                    : id == "wave_three_ready"
                        ? waveSystem.CreateState().Phase == WavePhase.Preparation
                            && waveSystem.CreateState().CurrentWaveNumber == 3
                    : id == "wave_four_ready"
                        ? waveSystem.CreateState().Phase == WavePhase.Preparation
                            && waveSystem.CreateState().CurrentWaveNumber == 4
                    : id == "wave_five_ready"
                        ? waveSystem.CreateState().Phase == WavePhase.Preparation
                            && waveSystem.CreateState().CurrentWaveNumber == 5
                    : id == "first_fire_hit"
                        ? findBurningEnemyIcon() != null
                    : id == "next_enemy_description_open"
                        ? isNextEnemyDescriptionVisible()
                    : id == "generator_placed"
                        ? towerNetwork.RegisteredTowerCount > authoredTowerCount
                    : id == "second_sink_placed"
                        ? findPlacedSink() != null
                    : id == "second_generator_placed"
                        ? findSecondGenerator() != null
                    : id == "fire_placed"
                        ? findPlacedFire() != null
                    : id == "second_generator_selected"
                        ? towerNetwork.SelectedTower == findSecondGenerator()?.GetComponent(
                            typeof(ITowerRuntimeView)) as ITowerRuntimeView
                    : id == "second_generator_unlinked"
                        ? findSecondGenerator() is TutorialTargetView generatorToUnlink
                            && findPlacedSink() is TutorialTargetView sinkToUnlink
                            && !towerNetwork.HasDirectLink(
                                generatorToUnlink.GetComponent(typeof(ITowerRuntimeView)) as ITowerRuntimeView,
                                sinkToUnlink.GetComponent(typeof(ITowerRuntimeView)) as ITowerRuntimeView)
                    : id == "new_generator_linked"
                        ? findPlacedGenerator() is TutorialTargetView generator
                            && towerNetwork.IsInValidChain(
                                generator.GetComponent(typeof(ITowerRuntimeView)) as ITowerRuntimeView)
                    : id == "second_generator_linked"
                        ? findSecondGenerator() is TutorialTargetView secondGenerator
                            && towerNetwork.IsInValidChain(
                                secondGenerator.GetComponent(typeof(ITowerRuntimeView)) as ITowerRuntimeView)
                    : id == "generator_linked_to_fire"
                        ? findSecondGenerator() is TutorialTargetView generatorToFire
                            && findPlacedFire() is TutorialTargetView fireTarget
                            && towerNetwork.HasDirectLink(
                                generatorToFire.GetComponent(typeof(ITowerRuntimeView)) as ITowerRuntimeView,
                                fireTarget.GetComponent(typeof(ITowerRuntimeView)) as ITowerRuntimeView)
                    : id == "fire_linked_to_sink"
                        ? findPlacedFire() is TutorialTargetView fireToSink
                            && findPlacedSink() is TutorialTargetView sinkTarget
                            && towerNetwork.HasDirectLink(
                                fireToSink.GetComponent(typeof(ITowerRuntimeView)) as ITowerRuntimeView,
                                sinkTarget.GetComponent(typeof(ITowerRuntimeView)) as ITowerRuntimeView)
                    : id == "generator_selected"
                        ? towerNetwork.SelectedTower?.CombatDefinition?.Family == TowerFamily.Generator
                        : id == "level_two_fire_selected"
                        ? towerNetwork.SelectedTower == findLevelTwoFire()
                        : id == "level_two_fire_upgraded"
                        ? towerNetwork.GetUpgradeLevel(findLevelTwoFire()) > 0
                        : id == "level_two_water_placed"
                        ? findLevelTwoPlacedWater() != null
                        : id == "level_two_fire_placed"
                        ? findLevelTwoPlacedFire() != null
                        : id == "level_two_sink_placed"
                        ? findLevelTwoPlacedSink() != null
                        : id == "level_two_generator_placed"
                        ? findLevelTwoPlacedGenerator() != null
                        : id == "level_two_generator_linked_to_fire"
                        ? towerNetwork.HasDirectLink(
                            findLevelTwoPlacedGenerator()?.GetComponent(typeof(ITowerRuntimeView))
                                as ITowerRuntimeView,
                            findLevelTwoFire())
                        : id == "level_two_fire_linked_to_water"
                        ? towerNetwork.HasDirectLink(
                            findLevelTwoFire(),
                            findLevelTwoPlacedWater()?.GetComponent(typeof(ITowerRuntimeView))
                                as ITowerRuntimeView)
                        : id == "level_two_water_linked_to_sink"
                        ? towerNetwork.HasDirectLink(
                            findLevelTwoPlacedWater()?.GetComponent(typeof(ITowerRuntimeView))
                                as ITowerRuntimeView,
                            findLevelTwoPlacedSink()?.GetComponent(typeof(ITowerRuntimeView))
                                as ITowerRuntimeView)
                        : id == "level_two_water_linked_to_fire"
                        ? towerNetwork.HasDirectLink(
                            findLevelTwoPlacedWater()?.GetComponent(typeof(ITowerRuntimeView))
                                as ITowerRuntimeView,
                            findLevelTwoPlacedFire()?.GetComponent(typeof(ITowerRuntimeView))
                                as ITowerRuntimeView)
                        : id == "level_two_fire_linked_to_sink"
                        ? towerNetwork.HasDirectLink(
                            findLevelTwoPlacedFire()?.GetComponent(typeof(ITowerRuntimeView))
                                as ITowerRuntimeView,
                            findLevelTwoPlacedSink()?.GetComponent(typeof(ITowerRuntimeView))
                                as ITowerRuntimeView)
                        : id == "generator_linked" && towerNetwork.HasValidChain,
                mode =>
                {
                    tutorialUiStage.SetMode(mode);
                    if (levelNumber == 2)
                    {
                        ApplyLevelTwoTutorialPlacementRules(
                            mode,
                            placementSystem,
                            towerNetwork,
                            waveSystem,
                            levelTwoWaterPlacementTarget,
                            levelTwoWaterPlacementCell,
                            levelTwoFirePlacementTarget,
                            levelTwoFirePlacementCell,
                            levelTwoSinkPlacementTarget,
                            levelTwoSinkPlacementCell,
                            levelTwoGeneratorPlacementTarget,
                            levelTwoGeneratorPlacementCell);
                        return;
                    }

                    towerNetwork.SetTutorialPlacementFree(
                        mode == TutorialGameplayUiMode.GeneratorOnly
                        || mode == TutorialGameplayUiMode.SinkPlacementOnly
                        || mode == TutorialGameplayUiMode.SecondGeneratorPlacementOnly
                        || mode == TutorialGameplayUiMode.FirePlacementOnly);

                    // Both calls are idempotent, so running them on every mode change is safe.
                    // Freezing with nothing to point at is not, so the beat is skipped when the
                    // first Fire-hit enemy's icon cannot be resolved.
                    if (mode == TutorialGameplayUiMode.BurnStatusFrozen)
                    {
                        Transform burningIcon = findBurningEnemyIcon();
                        if (burningIcon != null)
                        {
                            focusSystem.Enter(burningIcon.position);
                        }
                    }
                    else
                    {
                        focusSystem.Exit();
                    }

                    if (mode == TutorialGameplayUiMode.GeneratorOnly && placementTarget != null)
                    {
                        placementSystem.SetRequiredPlacement(
                            placementCell,
                            new TowerFootprint(2, 2, 2));
                    }
                    else if (mode == TutorialGameplayUiMode.SinkPlacementOnly && sinkPlacementTarget != null)
                    {
                        placementSystem.SetRequiredPlacement(
                            sinkPlacementCell,
                            new TowerFootprint(2, 2, 2));
                    }
                    else if (mode == TutorialGameplayUiMode.SecondGeneratorPlacementOnly
                        && secondGeneratorPlacementTarget != null)
                    {
                        placementSystem.SetRequiredPlacement(
                            secondGeneratorPlacementCell,
                            new TowerFootprint(2, 2, 2));
                    }
                    else if (mode == TutorialGameplayUiMode.FirePlacementOnly
                        && firePlacementTarget != null)
                    {
                        placementSystem.ClearReservedPlacement();
                        placementSystem.SetRequiredPlacement(
                            firePlacementCell,
                            new TowerFootprint(2, 2, 2));
                    }
                    else if (mode == TutorialGameplayUiMode.GeneratorAndSinkReady)
                    {
                        placementSystem.ClearRequiredPlacement();
                        placementSystem.SetReservedPlacement(
                            firePlacementCell,
                            new TowerFootprint(2, 2, 2));
                    }
                    else
                    {
                        placementSystem.ClearRequiredPlacement();
                    }
                },
                () => inputSystem.Current.HasPointerInput && inputSystem.Current.WasPressed));
        }

        private static void ApplyLevelTwoTutorialPlacementRules(
            TutorialGameplayUiMode mode,
            GridPlacementSystem placementSystem,
            TowerNetworkSystem towerNetwork,
            IWaveSystem waveSystem,
            Transform waterTarget,
            GridCell waterCell,
            Transform fireTarget,
            GridCell fireCell,
            Transform sinkTarget,
            GridCell sinkCell,
            Transform generatorTarget,
            GridCell generatorCell)
        {
            var footprint = new TowerFootprint(2, 2, 2);
            GridPlacementConstraint water = new GridPlacementConstraint(waterCell, footprint);
            GridPlacementConstraint fire = new GridPlacementConstraint(fireCell, footprint);
            GridPlacementConstraint sink = new GridPlacementConstraint(sinkCell, footprint);
            GridPlacementConstraint generator = new GridPlacementConstraint(generatorCell, footprint);
            bool isWaterPlacement = mode == TutorialGameplayUiMode.LevelTwoWaterPlacement;
            bool isFirePlacement = mode == TutorialGameplayUiMode.LevelTwoFirePlacement;
            bool isSinkPlacement = mode == TutorialGameplayUiMode.LevelTwoSinkPlacement;
            bool isGeneratorPlacement = mode == TutorialGameplayUiMode.LevelTwoGeneratorPlacement;

            towerNetwork.SetTutorialPlacementFree(
                isWaterPlacement || isFirePlacement || isSinkPlacement || isGeneratorPlacement);
            placementSystem.ClearRequiredPlacement();

            if (isWaterPlacement && waterTarget != null)
            {
                placementSystem.SetReservedPlacements(fire, sink, generator);
                placementSystem.SetRequiredPlacement(waterCell, footprint);
                return;
            }

            if (isFirePlacement && fireTarget != null)
            {
                placementSystem.SetReservedPlacements(water, sink, generator);
                placementSystem.SetRequiredPlacement(fireCell, footprint);
                return;
            }

            if (isSinkPlacement && sinkTarget != null)
            {
                placementSystem.SetReservedPlacements(water, fire, generator);
                placementSystem.SetRequiredPlacement(sinkCell, footprint);
                return;
            }

            if (isGeneratorPlacement && generatorTarget != null)
            {
                placementSystem.SetReservedPlacements(water, fire, sink);
                placementSystem.SetRequiredPlacement(generatorCell, footprint);
                return;
            }

            if (mode != TutorialGameplayUiMode.Full
                || waveSystem.CreateState().CurrentWaveNumber <= 2)
            {
                placementSystem.SetReservedPlacements(water, fire, sink, generator);
                return;
            }

            placementSystem.ClearReservedPlacement();
        }

        private static TutorialTargetView GetRuntimeTutorialTarget(
            string id,
            Func<TutorialTargetView> firstGenerator,
            Func<TutorialTargetView> sink,
            Func<TutorialTargetView> secondGenerator,
            Func<TutorialTargetView> fire,
            Func<TutorialTargetView> levelTwoGenerator,
            Func<TutorialTargetView> levelTwoWater,
            Func<TutorialTargetView> levelTwoFire,
            Func<TutorialTargetView> levelTwoSink,
            Func<TutorialTargetView> burningEnemyIcon,
            IReadOnlyDictionary<string, TutorialTargetView> targets)
        {
            TutorialTargetView target = id == "tutorial_generator" ? firstGenerator()
                : id == "tutorial_sink" ? sink()
                : id == "tutorial_second_generator" ? secondGenerator()
                : id == "tutorial_fire" ? fire()
                : id == "tutorial_level_two_generator" ? levelTwoGenerator()
                : id == "tutorial_level_two_water" ? levelTwoWater()
                : id == "tutorial_level_two_fire" ? levelTwoFire()
                : id == "tutorial_level_two_sink" ? levelTwoSink()
                : id == "burning_enemy_icon" ? burningEnemyIcon()
                : targets.TryGetValue(id, out TutorialTargetView found) ? found : null;
            if (target != null)
            {
                target.SetTargetId(id);
            }

            return target;
        }

        private static void RegisterTutorialTarget(
            Transform transform,
            string id,
            IDictionary<string, TutorialTargetView> targets)
        {
            if (transform == null)
            {
                return;
            }

            TutorialTargetView target = transform.GetComponent<TutorialTargetView>()
                ?? transform.gameObject.AddComponent<TutorialTargetView>();
            target.SetTargetId(id);
            targets[id] = target;
        }

        private static Transform CreatePlacementTarget(
            BoardView boardView,
            GridCell anchor,
            string targetName,
            out GridCell placementCell)
        {
            placementCell = default;
            if (boardView?.Board == null)
            {
                return null;
            }

            GridCoordinateMapper mapper = new GridCoordinateMapper(
                boardView.Board.Dimensions,
                boardView.Board.CellSize,
                boardView.Board.HeightUnit,
                boardView.WorldOrigin);
            var placementFootprint = new TowerFootprint(2, 2, 2);
            if (!IsBuildableFootprint(boardView.Board, anchor, placementFootprint))
            {
                return null;
            }

            var target = new GameObject(targetName).transform;
            target.SetParent(boardView.transform, false);
            target.position = mapper.FootprintBottomCenter(anchor, placementFootprint);
            TutorialTargetView targetView = target.gameObject.AddComponent<TutorialTargetView>();
            targetView.SetWorldBoundsSize(new Vector3(
                placementFootprint.Width * boardView.Board.CellSize,
                0.1f,
                placementFootprint.Depth * boardView.Board.CellSize));
            placementCell = anchor;
            return target;
        }

        private static bool IsBuildableFootprint(
            BoardDefinition board,
            GridCell anchor,
            TowerFootprint footprint)
        {
            for (int zOffset = 0; zOffset < footprint.Depth; zOffset++)
            {
                for (int xOffset = 0; xOffset < footprint.Width; xOffset++)
                {
                    var cell = new GridCell(anchor.X + xOffset, anchor.Z + zOffset, anchor.Y);
                    bool isBuildable = false;
                    for (int index = 0; index < board.Cells.Count; index++)
                    {
                        BoardCellDefinition definition = board.Cells[index];
                        if (definition.Coordinate != cell)
                        {
                            continue;
                        }

                        isBuildable = definition.SupportsPlacement
                            && definition.IsBuildable
                            && !definition.IsRoad
                            && !definition.IsStaticBlocker;
                        break;
                    }

                    if (!isBuildable)
                    {
                        return false;
                    }
                }
            }

            return true;
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
            WaveSystem waveSystem = container.Resolve<WaveSystem>();
            container.Resolve<LevelOutcomeHudPresenter>().BindLevel(
                hasNextLevel,
                () =>
                {
                    if (!waveSystem.RetryCurrentWave())
                    {
                        gameFlowSystem.RequestReplayLevel(currentLevelNumber);
                    }
                },
                () => gameFlowSystem.RequestPlayNextLevel(currentLevelNumber),
                gameFlowSystem.RequestReturnToLevelMenu,
                stars => gameFlowSystem.ReportLevelCleared(currentLevelNumber, stars),
                LevelStarRating.AwardsFullStars(levelNumber));
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
