namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Owns non-blocking save-warning presentation and retry callbacks.
    /// </summary>
    public sealed class SaveRecoveryFlow
    {
        private readonly SaveSystem saveSystem;
        private readonly ApplicationUISystem applicationUiSystem;

        public SaveRecoveryFlow(SaveSystem saveSystem, ApplicationUISystem applicationUiSystem)
        {
            this.saveSystem = saveSystem;
            this.applicationUiSystem = applicationUiSystem;
        }

        public void ShowWarning(string error)
        {
            string message = string.IsNullOrWhiteSpace(error)
                ? "Progress is unlocked for this session but has not been saved."
                : error;
            applicationUiSystem.ShowSaveWarning(message, RetrySave);
        }

        private void RetrySave()
        {
            SaveWriteResult result = saveSystem.RetrySave();
            if (result.IsSuccess)
            {
                applicationUiSystem.HideSaveWarning();
            }
            else
            {
                ShowWarning(result.Error);
            }
        }

    }
}
