using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Owns the single local autosave slot and its bounded recovery files.
    /// Domain interpretation remains in <see cref="SaveSystem"/>.
    /// </summary>
    public sealed class LocalSaveRepository : ISaveRepository
    {
        public const string ProductDirectoryName = "TowerDefense3D";
        public const string SaveDirectoryName = "Saves";
        public const string PrimaryFileName = "autosave.json";
        public const string BackupFileName = "autosave.backup.json";
        public const string TemporarySearchPattern = "autosave.*.tmp";

        private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false, true);

        private readonly string saveRoot;
        private readonly string primaryPath;
        private readonly string backupPath;

        public LocalSaveRepository(string persistentDataPath)
        {
            if (string.IsNullOrWhiteSpace(persistentDataPath))
            {
                throw new ArgumentException("Persistent data path is required.", nameof(persistentDataPath));
            }

            string productRoot = Path.Combine(persistentDataPath, ProductDirectoryName);
            saveRoot = Path.GetFullPath(Path.Combine(productRoot, SaveDirectoryName));
            primaryPath = GetOwnedPath(PrimaryFileName);
            backupPath = GetOwnedPath(BackupFileName);
        }

        public string SaveRoot => saveRoot;
        public string PrimaryPath => primaryPath;
        public string BackupPath => backupPath;

        public SaveLoadResult Load()
        {
            SaveLoadResult primary = TryLoadCandidate(primaryPath);
            if (primary.IsSuccess)
            {
                return primary;
            }

            SaveLoadResult backup = TryLoadCandidate(backupPath);
            if (backup.IsSuccess)
            {
                return backup;
            }

            if (primary.Status == SaveLoadStatus.Missing && backup.Status == SaveLoadStatus.Missing)
            {
                return new SaveLoadResult(SaveLoadStatus.Missing, null, "No autosave exists yet.");
            }

            if (primary.Status == SaveLoadStatus.Incompatible || backup.Status == SaveLoadStatus.Incompatible)
            {
                return new SaveLoadResult(
                    SaveLoadStatus.Incompatible,
                    null,
                    CombineErrors(primary.Error, backup.Error));
            }

            if (primary.Status == SaveLoadStatus.Corrupt || backup.Status == SaveLoadStatus.Corrupt)
            {
                return new SaveLoadResult(
                    SaveLoadStatus.Corrupt,
                    null,
                    CombineErrors(primary.Error, backup.Error));
            }

            if (primary.Status == SaveLoadStatus.Unavailable || backup.Status == SaveLoadStatus.Unavailable)
            {
                return new SaveLoadResult(
                    SaveLoadStatus.Unavailable,
                    null,
                    CombineErrors(primary.Error, backup.Error));
            }

            return new SaveLoadResult(
                SaveLoadStatus.Unexpected,
                null,
                CombineErrors(primary.Error, backup.Error));
        }

        public SaveWriteResult Save(SaveSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return new SaveWriteResult(
                    SaveWriteStatus.ValidationFailed,
                    "Save data is missing.");
            }

            if (!snapshot.TryValidate(out string validationError))
            {
                return new SaveWriteResult(
                    SaveWriteStatus.ValidationFailed,
                    validationError);
            }

            string temporaryPath = null;
            try
            {
                Directory.CreateDirectory(saveRoot);
                temporaryPath = GetOwnedPath($"autosave.{Guid.NewGuid():N}.tmp");

                string json = JsonUtility.ToJson(snapshot, true);
                WriteAndFlush(temporaryPath, json);

                SaveLoadResult staged = TryLoadCandidate(temporaryPath);
                if (!staged.IsSuccess)
                {
                    return new SaveWriteResult(
                        SaveWriteStatus.ValidationFailed,
                        $"Staged save validation failed: {staged.Error}");
                }

                CommitTemporaryFile(temporaryPath);
                temporaryPath = null;
                return new SaveWriteResult(SaveWriteStatus.Success, string.Empty);
            }
            catch (UnauthorizedAccessException exception)
            {
                return new SaveWriteResult(SaveWriteStatus.Unavailable, exception.Message);
            }
            catch (IOException exception)
            {
                return new SaveWriteResult(SaveWriteStatus.Unavailable, exception.Message);
            }
            catch (NotSupportedException exception)
            {
                return new SaveWriteResult(SaveWriteStatus.Unavailable, exception.Message);
            }
            catch (Exception exception)
            {
                return new SaveWriteResult(SaveWriteStatus.Unexpected, exception.Message);
            }
            finally
            {
                if (!string.IsNullOrEmpty(temporaryPath))
                {
                    TryDeleteFile(temporaryPath);
                }
            }
        }

        public SaveWriteResult DeleteOwnedAutosave()
        {
            try
            {
                TryDeleteFile(primaryPath);
                TryDeleteFile(backupPath);

                if (Directory.Exists(saveRoot))
                {
                    string[] temporaryFiles = Directory.GetFiles(
                        saveRoot,
                        TemporarySearchPattern,
                        SearchOption.TopDirectoryOnly);

                    for (int index = 0; index < temporaryFiles.Length; index++)
                    {
                        TryDeleteFile(temporaryFiles[index]);
                    }
                }

                return new SaveWriteResult(SaveWriteStatus.Success, string.Empty);
            }
            catch (UnauthorizedAccessException exception)
            {
                return new SaveWriteResult(SaveWriteStatus.Unavailable, exception.Message);
            }
            catch (IOException exception)
            {
                return new SaveWriteResult(SaveWriteStatus.Unavailable, exception.Message);
            }
            catch (Exception exception)
            {
                return new SaveWriteResult(SaveWriteStatus.Unexpected, exception.Message);
            }
        }

        private SaveLoadResult TryLoadCandidate(string path)
        {
            if (!File.Exists(path))
            {
                return new SaveLoadResult(SaveLoadStatus.Missing, null, $"'{Path.GetFileName(path)}' is missing.");
            }

            try
            {
                string json = File.ReadAllText(path, Utf8WithoutBom);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new SaveLoadResult(SaveLoadStatus.Corrupt, null, $"'{Path.GetFileName(path)}' is empty.");
                }

                if (!ContainsRequiredFields(json))
                {
                    return new SaveLoadResult(
                        SaveLoadStatus.Corrupt,
                        null,
                        $"'{Path.GetFileName(path)}' is missing required save fields.");
                }

                SaveSnapshot snapshot;
                try
                {
                    snapshot = JsonUtility.FromJson<SaveSnapshot>(json);
                }
                catch (ArgumentException exception)
                {
                    return new SaveLoadResult(SaveLoadStatus.Corrupt, null, exception.Message);
                }

                if (snapshot == null)
                {
                    return new SaveLoadResult(
                        SaveLoadStatus.Corrupt,
                        null,
                        $"'{Path.GetFileName(path)}' has no save root.");
                }

                if (snapshot.SchemaVersion != SaveSnapshot.CurrentSchemaVersion)
                {
                    return new SaveLoadResult(
                        SaveLoadStatus.Incompatible,
                        null,
                        $"'{Path.GetFileName(path)}' uses unsupported schema {snapshot.SchemaVersion}.");
                }

                if (!snapshot.TryValidate(out string validationError))
                {
                    return new SaveLoadResult(SaveLoadStatus.Corrupt, null, validationError);
                }

                return new SaveLoadResult(SaveLoadStatus.Success, snapshot, string.Empty);
            }
            catch (UnauthorizedAccessException exception)
            {
                return new SaveLoadResult(SaveLoadStatus.Unavailable, null, exception.Message);
            }
            catch (IOException exception)
            {
                return new SaveLoadResult(SaveLoadStatus.Unavailable, null, exception.Message);
            }
            catch (Exception exception)
            {
                return new SaveLoadResult(SaveLoadStatus.Unexpected, null, exception.Message);
            }
        }

        private void CommitTemporaryFile(string temporaryPath)
        {
            if (!File.Exists(primaryPath))
            {
                File.Move(temporaryPath, primaryPath);
                return;
            }

            try
            {
                File.Replace(temporaryPath, primaryPath, backupPath);
            }
            catch (PlatformNotSupportedException)
            {
                CommitWithMoveFallback(temporaryPath);
            }
            catch (NotSupportedException)
            {
                CommitWithMoveFallback(temporaryPath);
            }
            catch (IOException)
            {
                CommitWithMoveFallback(temporaryPath);
            }
        }

        private void CommitWithMoveFallback(string temporaryPath)
        {
            TryDeleteFile(backupPath);
            File.Move(primaryPath, backupPath);

            try
            {
                File.Move(temporaryPath, primaryPath);
            }
            catch
            {
                if (!File.Exists(primaryPath) && File.Exists(backupPath))
                {
                    File.Copy(backupPath, primaryPath);
                }

                throw;
            }
        }

        private static void WriteAndFlush(string path, string content)
        {
            byte[] bytes = Utf8WithoutBom.GetBytes(content);
            using (var stream = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }

        private string GetOwnedPath(string fileName)
        {
            return Path.GetFullPath(Path.Combine(saveRoot, fileName));
        }

        private static void TryDeleteFile(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static string CombineErrors(string first, string second)
        {
            if (string.IsNullOrWhiteSpace(first))
            {
                return second;
            }

            if (string.IsNullOrWhiteSpace(second))
            {
                return first;
            }

            return $"{first} {second}";
        }

        private static bool ContainsRequiredFields(string json)
        {
            return json.IndexOf("\"schemaVersion\"", StringComparison.Ordinal) >= 0
                && json.IndexOf("\"slotId\"", StringComparison.Ordinal) >= 0
                && json.IndexOf("\"unlockedLevelNumbers\"", StringComparison.Ordinal) >= 0;
        }
    }
}
