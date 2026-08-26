using System;
using TowerDefense3D.Towers;
using UnityEngine;

namespace TowerDefense3D.GameFlow
{
    public readonly struct TowerPlacementPointerEvent
    {
        public TowerPlacementPointerEvent(int pointerId, Vector2 screenPosition, bool isOverUi)
        {
            PointerId = pointerId;
            ScreenPosition = screenPosition;
            IsOverUi = isOverUi;
        }

        public int PointerId { get; }
        public Vector2 ScreenPosition { get; }
        public bool IsOverUi { get; }
    }

    public interface ITowerNetworkHudView
    {
        event Action<TowerCombatDefinition, TowerPlacementPointerEvent> TowerDragBegan;
        event Action<TowerPlacementPointerEvent> TowerDragMoved;
        event Action<TowerPlacementPointerEvent> TowerDragEnded;
        event Action<int> TowerDragCanceled;
        event Action UnlinkRequested;
        event Action CancelPlacementRequested;
        event Action ReturnToMenuRequested;

        void Initialize();
        void Render(TowerNetworkHudState state);
        void Show();
    }
}
