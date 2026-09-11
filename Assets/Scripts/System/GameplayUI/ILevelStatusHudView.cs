namespace TowerDefense3D.GameFlow
{
    public interface ILevelStatusHudView
    {
        void RenderGold(int gold);
        void RenderHealth(int currentHealth, int maximumHealth);
        void SetHealthVisible(bool visible);
    }

    public interface IWaveThreeDefeatHudView
    {
        event System.Action RetryWaveThreeRequested;

        void ShowWaveThreeDefeat();
        void HideWaveThreeDefeat();
    }
}
