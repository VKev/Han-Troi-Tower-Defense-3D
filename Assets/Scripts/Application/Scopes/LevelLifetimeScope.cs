using TowerDefense3D.GameplayInput;
using TowerDefense3D.GridPlacement;
using TowerDefense3D.Towers;
using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Child composition root for systems and Unity views owned by one additive level scene.
    /// </summary>
    public sealed class LevelLifetimeScope : LifetimeScope
    {
        private ActiveLevelSystemSlot activeLevelSystems;
        private LevelSystemGroup attachedSystems;

        protected override void Configure(IContainerBuilder builder)
        {
            LevelSceneContext levelContext = FindSceneComponent<LevelSceneContext>();
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
            builder.RegisterComponentInHierarchy<GridPlacementPresenter>();
            builder.RegisterComponentInHierarchy<TowerNetworkSceneAdapter>();
            builder.RegisterInstance(placementView.WorldCamera);
            builder.Register<BoardSystem>(Lifetime.Scoped);
            builder.Register<BoardCameraSystem>(Lifetime.Scoped);
            builder.Register<GameplayInputSystem>(Lifetime.Scoped);
            builder.Register<GridPlacementSystem>(Lifetime.Scoped);
            builder.Register<TowerNetworkSystem>(Lifetime.Scoped)
                .WithParameter("levelNumber", levelContext.LevelNumber);
            builder.Register<TowerInteractionSystem>(Lifetime.Scoped);
            builder.Register<TowerSimulationSystem>(Lifetime.Scoped);
            builder.Register<TowerLinkPresentationSystem>(Lifetime.Scoped);
            builder.Register<TowerProjectilePresentationSystem>(Lifetime.Scoped);
            builder.Register<LevelSystemGroup>(Lifetime.Scoped);
            builder.RegisterBuildCallback(AttachLevelSystems);
        }

        protected override void OnDestroy()
        {
            if (attachedSystems != null)
            {
                activeLevelSystems.DetachForScopeTeardown(attachedSystems);
                attachedSystems = null;
            }

            base.OnDestroy();
        }

        private void AttachLevelSystems(IObjectResolver container)
        {
            activeLevelSystems = container.Resolve<ActiveLevelSystemSlot>();
            GridPlacementSystem placementSystem = container.Resolve<GridPlacementSystem>();
            GridPlacementView placementView = container.Resolve<GridPlacementView>();
            container.Resolve<GridPlacementPresenter>().Bind(placementSystem, placementView);
            container.Resolve<TowerNetworkSceneAdapter>().Bind(container.Resolve<TowerNetworkSystem>());

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
