namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Defines the persistence boundary used by SaveSystem.
    /// </summary>
    public interface ISaveRepository
    {
        SaveLoadResult Load();
        SaveWriteResult Save(SaveSnapshot snapshot);
        SaveWriteResult DeleteOwnedAutosave();
    }
}
