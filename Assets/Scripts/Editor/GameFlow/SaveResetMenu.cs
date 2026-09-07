using System.IO;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.GameFlow.Editor
{
    /// <summary>
    /// Deletes the local autosave so the next run starts with only level one unlocked.
    /// </summary>
    /// <remarks>
    /// Progress lives in a JSON file under the player's persistent data path, not in PlayerPrefs,
    /// so clearing PlayerPrefs does nothing for it. Deletion goes through the repository that owns
    /// the slot rather than unlinking files here, so the backup and any half-written temporary
    /// files go with it.
    /// </remarks>
    public static class SaveResetMenu
    {
        private const string MenuPath = "Tools/Tower Defense/Reset Save Progress";

        [MenuItem(MenuPath)]
        public static void ResetSaveProgress()
        {
            string folder = GetSaveFolder();
            string summary = DescribeCurrentSave(folder);

            if (!EditorUtility.DisplayDialog(
                    "Reset save progress",
                    summary
                        + "\n\nDeleting this leaves only level one unlocked. It cannot be undone.",
                    "Delete",
                    "Cancel"))
            {
                return;
            }

            var repository = new LocalSaveRepository(Application.persistentDataPath);
            SaveWriteResult result = repository.DeleteOwnedAutosave();
            if (!result.IsSuccess)
            {
                Debug.LogError("Could not delete the autosave: " + result.Error);
                return;
            }

            Debug.Log(
                "Save progress reset. The next run starts fresh with level one unlocked.\n"
                + folder);

            if (Application.isPlaying)
            {
                // The running session still holds its own UnlockProgress and will write it back
                // the next time a level unlocks, putting the file straight back.
                Debug.LogWarning(
                    "Play mode is running: the live session still holds the old progress and will "
                    + "save it again. Leave play mode and re-enter for the reset to take effect.");
            }
        }

        private static string GetSaveFolder()
        {
            return Path.Combine(
                Application.persistentDataPath,
                LocalSaveRepository.ProductDirectoryName,
                LocalSaveRepository.SaveDirectoryName);
        }

        /// <summary>
        /// What is about to be deleted, so the dialog is not a blind confirmation.
        /// </summary>
        private static string DescribeCurrentSave(string folder)
        {
            if (!Directory.Exists(folder))
            {
                return "No save folder exists yet:\n" + folder;
            }

            string[] files = Directory.GetFiles(folder);
            if (files.Length == 0)
            {
                return "The save folder is already empty:\n" + folder;
            }

            var description = new System.Text.StringBuilder();
            description.AppendLine(folder);
            for (int index = 0; index < files.Length; index++)
            {
                var info = new FileInfo(files[index]);
                description.AppendLine("  " + info.Name + "  (" + info.Length + " bytes)");
            }

            return description.ToString();
        }
    }
}
