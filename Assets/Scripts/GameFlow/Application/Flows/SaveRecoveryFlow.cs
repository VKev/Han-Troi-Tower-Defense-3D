namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Owns non-blocking save-warning presentation and retry callbacks.
    /// </summary>
    public sealed class SaveRecoveryFlow
    {
        private readonly SaveSystem saveSystem;
        private readonly IApplicationUIController applicationUi;

        public SaveRecoveryFlow(SaveSystem saveSystem, IApplicationUIController applicationUi)
        {
            this.saveSystem = saveSystem;
            this.applicationUi = applicationUi;
        }

        public void ShowWarning(string error)
        {
            string message = string.IsNullOrWhiteSpace(error)
                ? "Progress is unlocked for this session but has not been saved."
                : error;
            applicationUi.ShowSaveWarning(message, RetrySave);
        }

        private void RetrySave()
        {
            SaveWriteResult result = saveSystem.RetrySave();
            if (result.IsSuccess)
            {
                applicationUi.HideSaveWarning();
            }
            else
            {
                ShowWarning(result.Error);
            }
        }

    }
}
