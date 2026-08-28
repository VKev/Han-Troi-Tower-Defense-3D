namespace TowerDefense3D.GameFlow
{
    public interface ILevelStatusHudView
    {
        void RenderGold(int gold);
        void RenderHealth(int currentHealth, int maximumHealth);
    }
}
