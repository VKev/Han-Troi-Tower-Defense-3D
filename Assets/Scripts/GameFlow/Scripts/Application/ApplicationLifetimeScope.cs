using System;
using TowerDefense3D.Towers;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Bootstrap composition root for application-owned services and Unity adapters.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ApplicationLifetimeScope : LifetimeScope
    {
        [SerializeField] private LevelCatalog levelCatalog;
        [SerializeField] private TowerCatalog towerCatalog;
        [SerializeField] private LevelSceneLoader levelSceneLoader;
        [SerializeField] private ApplicationUIManager applicationUiManager;

        protected override void Configure(IContainerBuilder builder)
        {
            if (levelCatalog == null
                || towerCatalog == null
                || levelSceneLoader == null
                || applicationUiManager == null)
            {
                throw new InvalidOperationException(
                    "ApplicationLifetimeScope requires LevelCatalog, TowerCatalog, "
                    + "LevelSceneLoader, and ApplicationUIManager.");
            }

            builder.RegisterInstance(levelCatalog);
            builder.RegisterInstance(towerCatalog);
            builder.Register<TowerNetworkManager>(Lifetime.Singleton);
            builder.RegisterInstance(new LocalSaveRepository(Application.persistentDataPath));
            builder.Register<SaveCoordinator>(Lifetime.Singleton)
                .WithParameter("applicationVersion", Application.version);
            builder.RegisterComponent(levelSceneLoader);
            builder.RegisterComponent(applicationUiManager)
                .As<IApplicationUIController>();
            builder.RegisterEntryPoint<GameFlowCoordinator>();
        }
    }
}
