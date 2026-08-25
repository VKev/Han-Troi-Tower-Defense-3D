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
            bool cancelPlacementEnabled)
        {
            SelectedText = selectedText;
            ChainText = chainText;
            QueueText = queueText;
            FeedbackText = feedbackText;
            TowerSelectionEnabled = towerSelectionEnabled;
            UnlinkEnabled = unlinkEnabled;
            CancelPlacementEnabled = cancelPlacementEnabled;
        }

        public string SelectedText { get; }
        public string ChainText { get; }
        public string QueueText { get; }
        public string FeedbackText { get; }
        public bool TowerSelectionEnabled { get; }
        public bool UnlinkEnabled { get; }
        public bool CancelPlacementEnabled { get; }
    }
}
