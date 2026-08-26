using System;
using TowerDefense3D.Enemies;
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
            builder.RegisterComponentInHierarchy<PlacementHudView>()
                .As<IPlacementHudView>();
            builder.RegisterComponentInHierarchy<TowerNetworkHudView>()
                .As<ITowerNetworkHudView>();
            builder.RegisterComponentInHierarchy<WaveHudView>()
                .As<IWaveHudView>();
            builder.RegisterComponentInHierarchy<EnemyViewPool>()
                .As<IEnemyViewPool>();
            builder.RegisterComponentInHierarchy<GridPlacementPresenter>();
            builder.RegisterInstance(placementView.WorldCamera);
            builder.RegisterInstance(waveSchedule);
            builder.Register<BoardSystem>(Lifetime.Scoped);
            builder.Register<RoadPath>(
                resolver => RoadPathFactory.Create(resolver.Resolve<BoardSystem>()),
                Lifetime.Scoped);
            builder.Register<BoardCameraSystem>(Lifetime.Scoped);
            builder.Register<GameplayInputSystem>(Lifetime.Scoped);
            builder.Register<GridPlacementSystem>(Lifetime.Scoped);
            builder.Register<TowerNetworkSystem>(Lifetime.Scoped)
                .WithParameter("levelNumber", levelNumber);
            builder.Register<TowerInteractionSystem>(Lifetime.Scoped);
            builder.Register<EnemySystem>(Lifetime.Scoped);
            builder.Register<WaveSpawnPlanner>(Lifetime.Scoped);
            builder.Register<WaveSystem>(Lifetime.Scoped)
                .AsSelf()
                .As<IWaveSystem>();
            builder.Register<CombatTimelinePlanner>(Lifetime.Scoped);
            builder.Register<CombatTimelineSystem>(Lifetime.Scoped);
            builder.Register<GameplaySimulationSystem>(Lifetime.Scoped);
            builder.Register<EnemyPresentationSystem>(Lifetime.Scoped);
            builder.Register<TowerLinkPresentationSystem>(Lifetime.Scoped);
            builder.Register<TowerProjectilePresentationSystem>(Lifetime.Scoped);
            builder.Register<TowerNetworkHudPresenter>(Lifetime.Scoped);
            builder.Register<WaveHudPresenter>(Lifetime.Scoped);
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
            container.Resolve<GameplayUISystem>()
                .BindReturnToMenu(container.Resolve<GameFlowSystem>().RequestReturnToLevelMenu);

            LevelSystemGroup systems = container.Resolve<LevelSystemGroup>();
            systems.Start();
            activeLevelSystems.Attach(systems);
            attachedSystems = systems;
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
    }
}
