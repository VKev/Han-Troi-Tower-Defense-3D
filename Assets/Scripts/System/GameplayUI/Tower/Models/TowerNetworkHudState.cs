using UnityEngine;

namespace TowerDefense3D.GameFlow
{
    public readonly struct TowerNetworkHudState
    {
        public TowerNetworkHudState(
            string selectedText,
            string feedbackText,
            bool towerSelectionEnabled,
            bool unlinkEnabled,
            bool sellEnabled,
            bool towerActionsVisible,
            Vector2 towerActionsScreenPosition,
            bool upgradeEnabled = false,
            string upgradeCostText = "",
            string sellRefundText = "",
            bool upgradeShowsPrice = false)
        {
            SelectedText = selectedText;
            FeedbackText = feedbackText;
            TowerSelectionEnabled = towerSelectionEnabled;
            UnlinkEnabled = unlinkEnabled;
            SellEnabled = sellEnabled;
            TowerActionsVisible = towerActionsVisible;
            TowerActionsScreenPosition = towerActionsScreenPosition;
            UpgradeEnabled = upgradeEnabled;
            UpgradeCostText = upgradeCostText;
            SellRefundText = sellRefundText;
            UpgradeShowsPrice = upgradeShowsPrice;
        }

        public string SelectedText { get; }
        public string FeedbackText { get; }
        public bool TowerSelectionEnabled { get; }
        public bool UnlinkEnabled { get; }
        public bool SellEnabled { get; }

        /// <summary>
        /// Where the selected tower sits on screen, so its actions can be shown over its head.
        /// Hidden when nothing is selected or the tower is behind the camera.
        /// </summary>
        public bool TowerActionsVisible { get; }
        public Vector2 TowerActionsScreenPosition { get; }

        /// <summary>
        /// Whether the upgrade can actually be bought right now: the tower has a level left and
        /// the purse covers it.
        /// </summary>
        public bool UpgradeEnabled { get; }

        /// <summary>Price on the upgrade button, or "MAX" when there is nothing left to buy.</summary>
        public string UpgradeCostText { get; }

        /// <summary>What selling hands back, printed on the sell button.</summary>
        public string SellRefundText { get; }

        /// <summary>
        /// Whether the upgrade button is quoting a price the player could pay.
        /// </summary>
        /// <remarks>
        /// False both when the tower has no upgrade at all and when it is already maxed out. The
        /// coin rides on this rather than on the text being non-empty, because "MAX" is a state,
        /// not a price, and a coin beside it would read as one.
        /// </remarks>
        public bool UpgradeShowsPrice { get; }
    }
}
