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
        public GameplayCameraGestureSnapshot CameraGesture { get; private set; }
        public GameplayInputMode Mode { get; private set; }

        public void Start()
        {
            Capture();
        }

        public void Tick()
        {
            Capture();
        }

        public void SetMode(GameplayInputMode mode)
        {
            Mode = mode;
        }

        private void Capture()
        {
            Current = source.Capture();
            CameraGesture = source.CaptureCameraGesture();
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
