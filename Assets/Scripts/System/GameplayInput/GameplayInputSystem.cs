using System;

namespace TowerDefense3D.GameplayInput
{
    /// <summary>
    /// Samples the Unity input boundary once and exposes the same snapshot to every gameplay system.
    /// </summary>
    public sealed class GameplayInputSystem
    {
        private readonly IGameplayInputSource source;

        public GameplayInputSystem(IGameplayInputSource source)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public GameplayInputSnapshot Current { get; private set; }
        public GameplayInputMode Mode { get; private set; }

        public void Start()
        {
            Current = source.Capture();
        }

        public void Tick()
        {
            Current = source.Capture();
        }

        public void SetMode(GameplayInputMode mode)
        {
            Mode = mode;
        }

        public void ClearMode(GameplayInputMode mode)
        {
            if (Mode == mode)
            {
                Mode = GameplayInputMode.None;
            }
        }
    }
}
