namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Owns non-blocking save-warning presentation and retry callbacks.
    /// </summary>
    public sealed class SaveRecoveryFlow
    {
        private readonly SaveCoordinator saveCoordinator;
        private readonly IApplicationUIController applicationUi;

        public SaveRecoveryFlow(SaveCoordinator saveCoordinator, IApplicationUIController applicationUi)
        {
            this.saveCoordinator = saveCoordinator;
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
            SaveWriteResult result = saveCoordinator.RetrySave();
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
