namespace TowerDefense3D.GameplayInput
{
    public interface IGameplayInputSource
    {
        GameplayInputSnapshot Capture();
    }
}
