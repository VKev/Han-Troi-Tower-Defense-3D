using TowerDefense3D.GameplayInput;
using TowerDefense3D.GridPlacement;
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
            builder.RegisterComponentInHierarchy<GridPlacementPresenter>();
            builder.Register<BoardSystem>(Lifetime.Scoped);
            builder.Register<BoardCameraSystem>(Lifetime.Scoped);
            builder.Register<GameplayInputSystem>(Lifetime.Scoped);
            builder.Register<GridPlacementSystem>(Lifetime.Scoped);
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

            LevelSystemGroup systems = container.Resolve<LevelSystemGroup>();
            systems.Start();
            activeLevelSystems.Attach(systems);
            attachedSystems = systems;
        }
    }
}
