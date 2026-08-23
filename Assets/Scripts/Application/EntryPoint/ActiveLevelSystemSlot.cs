using System;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Holds the one level system group currently owned by an additive level scope.
    /// </summary>
    public sealed class ActiveLevelSystemSlot
    {
        private LevelSystemGroup activeSystems;

        public bool HasActiveLevel => activeSystems != null;

        public void Attach(LevelSystemGroup systems)
        {
            if (systems == null)
            {
                throw new ArgumentNullException(nameof(systems));
            }

            if (activeSystems != null)
            {
                throw new InvalidOperationException("An active level system group is already attached.");
            }

            activeSystems = systems;
        }

        public void Detach(LevelSystemGroup systems)
        {
            if (!ReferenceEquals(activeSystems, systems))
            {
                throw new InvalidOperationException("Only the attached level system group can be detached.");
            }

            activeSystems = null;
        }

        internal void DetachForScopeTeardown(LevelSystemGroup systems)
        {
            if (activeSystems == null)
            {
                return;
            }

            Detach(systems);
        }

        public void Tick(float deltaTime)
        {
            activeSystems?.Tick(deltaTime);
        }

        public void LateTick(float deltaTime)
        {
            activeSystems?.LateTick(deltaTime);
        }

        public void Clear()
        {
            activeSystems = null;
        }
    }
}
