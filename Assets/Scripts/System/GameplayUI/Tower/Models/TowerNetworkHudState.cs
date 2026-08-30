using UnityEngine;

namespace TowerDefense3D.GameFlow
{
    public readonly struct TowerNetworkHudState
    {
        public TowerNetworkHudState(
            string selectedText,
            string chainText,
            string queueText,
            string feedbackText,
            bool towerSelectionEnabled,
            bool unlinkEnabled,
            bool sellEnabled,
            bool cancelPlacementEnabled,
            bool towerActionsVisible,
            Vector2 towerActionsScreenPosition)
        {
            SelectedText = selectedText;
            ChainText = chainText;
            QueueText = queueText;
            FeedbackText = feedbackText;
            TowerSelectionEnabled = towerSelectionEnabled;
            UnlinkEnabled = unlinkEnabled;
            SellEnabled = sellEnabled;
            CancelPlacementEnabled = cancelPlacementEnabled;
            TowerActionsVisible = towerActionsVisible;
            TowerActionsScreenPosition = towerActionsScreenPosition;
        }

        public string SelectedText { get; }
        public string ChainText { get; }
        public string QueueText { get; }
        public string FeedbackText { get; }
        public bool TowerSelectionEnabled { get; }
        public bool UnlinkEnabled { get; }
        public bool SellEnabled { get; }
        public bool CancelPlacementEnabled { get; }

        /// <summary>
        /// Where the selected tower sits on screen, so its actions can be shown over its head.
        /// Hidden when nothing is selected or the tower is behind the camera.
        /// </summary>
        public bool TowerActionsVisible { get; }
        public Vector2 TowerActionsScreenPosition { get; }
    }
}
