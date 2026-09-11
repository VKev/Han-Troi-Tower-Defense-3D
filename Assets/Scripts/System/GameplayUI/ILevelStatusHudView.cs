namespace TowerDefense3D.GameFlow
{
    public interface ILevelStatusHudView
    {
        void RenderGold(int gold);
        void RenderHealth(int currentHealth, int maximumHealth);
        void SetHealthVisible(bool visible);

        /// <summary>
        /// Answers a purchase the player could not afford. The balance has not moved, so nothing
        /// in the ordinary render path would show them why the tap did nothing.
        /// </summary>
        void PlayPurchaseRefusedFeedback();
    }

    public interface IWaveThreeDefeatHudView
    {
        event System.Action RetryWaveThreeRequested;

        void ShowWaveThreeDefeat();
        void HideWaveThreeDefeat();
    }
}
