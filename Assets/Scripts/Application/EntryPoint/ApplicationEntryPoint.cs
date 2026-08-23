using System;
using System.Threading;
using UnityEngine;
using VContainer.Unity;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Sole VContainer lifecycle entry point for application and active-level systems.
    /// </summary>
    public sealed class ApplicationEntryPoint : IAsyncStartable, ITickable, ILateTickable, IDisposable
    {
        private readonly ApplicationSystemGroup applicationSystems;
        private readonly ActiveLevelSystemSlot activeLevelSystems;

        public ApplicationEntryPoint(
            ApplicationSystemGroup applicationSystems,
            ActiveLevelSystemSlot activeLevelSystems)
        {
            this.applicationSystems = applicationSystems;
            this.activeLevelSystems = activeLevelSystems;
        }

        public async Awaitable StartAsync(CancellationToken cancellation = default)
        {
            await Awaitable.MainThreadAsync();
            cancellation.ThrowIfCancellationRequested();
            applicationSystems.Start();
        }

        public void Tick()
        {
            float deltaTime = Time.deltaTime;
            applicationSystems.Tick();
            activeLevelSystems.Tick(deltaTime);
        }

        public void LateTick()
        {
            activeLevelSystems.LateTick(Time.deltaTime);
        }

        public void Dispose()
        {
            activeLevelSystems.Clear();
            applicationSystems.Shutdown();
        }
    }
}
