using System;
using System.Collections.Generic;
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
        event Action SellRequested;
        event Action UpgradeRequested;
        event Action ReturnToMenuRequested;

        void Initialize();

        /// <summary>
        /// Marks the towers the player has not earned yet. A locked tower still appears in the
        /// build bar, greyed out, so its existence reads as a goal rather than a missing button.
        /// </summary>
        void ApplyTowerLocks(IReadOnlyList<TowerCombatDefinition> lockedDefinitions);

        void Render(TowerNetworkHudState state);
        void Show();
    }
}
