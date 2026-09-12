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

        /// <summary>
        /// Dims the cards the player cannot currently pay for. They stay pressable.
        /// </summary>
        void ApplyTowerAffordability(IReadOnlyList<TowerCombatDefinition> unaffordableDefinitions);

        /// <summary>
        /// Replaces the price and coin with "MAX" on the cards the level already has its fill of,
        /// and puts the price back when one is sold.
        /// </summary>
        void ApplyTowerBuildLimits(IReadOnlyList<TowerCombatDefinition> maxedDefinitions);
        void SetTowerActionsAvailable(bool available);

        /// <summary>
        /// Whether the upgrade button belongs on the actions panel at all. Level 1 never offers
        /// it: upgrading is taught by its own beat at Level 2, and a third button the player has
        /// not been told about reads as one they are failing to use.
        /// </summary>
        void SetUpgradeAvailable(bool available);

        void Render(TowerNetworkHudState state);
        void Show();
    }
}
