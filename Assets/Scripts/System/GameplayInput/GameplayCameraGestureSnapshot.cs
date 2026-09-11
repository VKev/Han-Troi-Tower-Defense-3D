using UnityEngine;

namespace TowerDefense3D.GameplayInput
{
    /// <summary>
    /// Multi-touch and wheel input captured once for the current frame.
    /// </summary>
    /// <remarks>
    /// Kept apart from <see cref="GameplayInputSnapshot"/>, which models the single pointer every
    /// other gameplay system consumes. Widening that one to carry a second finger would change
    /// every construction site for the sake of the one system that needs two.
    /// </remarks>
    public readonly struct GameplayCameraGestureSnapshot
    {
        public GameplayCameraGestureSnapshot(
            bool isPinchActive,
            Vector2 pinchPositionA,
            Vector2 pinchPositionB,
            Vector2 scrollDelta)
        {
            IsPinchActive = isPinchActive;
            PinchPositionA = pinchPositionA;
            PinchPositionB = pinchPositionB;
            ScrollDelta = scrollDelta;
        }

        public bool IsPinchActive { get; }
        public Vector2 PinchPositionA { get; }
        public Vector2 PinchPositionB { get; }
        public Vector2 ScrollDelta { get; }
    }
}
