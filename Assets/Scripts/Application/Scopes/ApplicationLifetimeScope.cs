using System;
using TowerDefense3D.Enemies;
using TowerDefense3D.GridPlacement;
using TowerDefense3D.Mobile;
using TowerDefense3D.Towers;
using TowerDefense3D.Tutorials;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Bootstrap composition root for application systems and Unity integration boundaries.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ApplicationLifetimeScope : LifetimeScope
    {
        [SerializeField] private LevelCatalog levelCatalog;
        [SerializeField] private TowerCatalog towerCatalog;
        [SerializeField] private ElementReactionCatalog elementReactionCatalog;
        [SerializeField] private ApplicationUIView applicationUIView;

        protected override void Configure(IContainerBuilder builder)
        {

            var reactionErrors = elementReactionCatalog.CollectValidationErrors();
            if (reactionErrors.Count > 0)
            {
                throw new InvalidOperationException(string.Join("\n", reactionErrors));
            }

            builder.RegisterInstance(levelCatalog);
            builder.RegisterInstance(towerCatalog);
            builder.RegisterInstance(elementReactionCatalog);

            builder.Register<TowerNetworkManager>(Lifetime.Singleton);
            builder.Register<ISaveRepository>(_ => new LocalSaveRepository(Application.persistentDataPath),Lifetime.Singleton);
            builder.Register<SaveSystem>(
                resolver => new SaveSystem(
                    resolver.Resolve<ISaveRepository>(),
                    Application.version,
                    resolver.Resolve<TutorialProgress>()),
                Lifetime.Singleton);
            builder.Register<BootstrapSceneActivator>(Lifetime.Singleton);
            builder.Register<VContainerLevelSceneGateway>(Lifetime.Singleton).As<ILevelSceneGateway>();
            builder.Register<LevelSceneSystem>(Lifetime.Singleton);
            builder.Register<TutorialProgress>(Lifetime.Singleton);
            builder.Register<TutorialSystem>(resolver =>
            {
                var system = new TutorialSystem(resolver.Resolve<TutorialProgress>());
                system.Register(new FirstLinkTutorial());
                return system;
            }, Lifetime.Singleton);

            builder.RegisterComponent(applicationUIView).As<IApplicationUIView>();
            builder.RegisterComponentInHierarchy<TutorialOverlayView>().As<ITutorialOverlay>();
            builder.RegisterComponentInHierarchy<TutorialInputGate>().As<ITutorialInputGate>();
            
            builder.Register<ApplicationUISystem>(Lifetime.Singleton);
            builder.Register<ApplicationBootFlow>(Lifetime.Singleton);
            builder.Register<LevelMenuFlow>(Lifetime.Singleton);
            builder.Register<LevelTransitionFlow>(Lifetime.Singleton);
            builder.Register<SaveRecoveryFlow>(Lifetime.Singleton);
            builder.Register<GameFlowSystem>(Lifetime.Singleton);
            builder.Register<FramePacingSystem>(Lifetime.Singleton);

            builder.RegisterComponentInHierarchy<SafeAreaView>().As<ISafeAreaView>();
            
            builder.Register<SafeAreaSystem>(Lifetime.Singleton);
            builder.Register<ApplicationSystemGroup>(Lifetime.Singleton);
            builder.Register<ActiveLevelSystemSlot>(Lifetime.Singleton);
            
            builder.RegisterEntryPoint<ApplicationEntryPoint>();
        }
    }
}
