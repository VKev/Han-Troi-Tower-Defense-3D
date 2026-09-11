using System;
using System.Collections.Generic;

namespace TowerDefense3D.Tutorials
{
    [Serializable]
    public struct TutorialSaveRecord
    {
        [UnityEngine.SerializeField] private string tutorialId;

        public TutorialSaveRecord(string tutorialId) => this.tutorialId = tutorialId ?? string.Empty;
        public string TutorialId => tutorialId;
    }

    [Serializable]
    public sealed class TutorialProgress
    {
        public const string LevelOneTutorialCompleteId = "level_one_tutorial_complete";

        private readonly HashSet<string> completed = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> sessionCompleted = new HashSet<string>(StringComparer.Ordinal);

        public event Action PermanentProgressChanged;

        public bool HasCompletedLevelOneTutorial => completed.Contains(LevelOneTutorialCompleteId);

        public bool IsCompleted(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            return IsLevelOneTutorial(id) && HasCompletedLevelOneTutorial
                || sessionCompleted.Contains(id);
        }

        public void MarkCompleted(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                sessionCompleted.Add(id);
            }
        }

        public void CompleteLevelOneTutorial()
        {
            if (HasCompletedLevelOneTutorial)
            {
                return;
            }

            completed.Add(LevelOneTutorialCompleteId);
            sessionCompleted.Clear();
            PermanentProgressChanged?.Invoke();
        }

        public void ResetSession()
        {
            sessionCompleted.Clear();
        }

        public TutorialSaveRecord[] CreateSnapshot()
        {
            var records = new TutorialSaveRecord[completed.Count];
            int index = 0;
            foreach (string id in completed) records[index++] = new TutorialSaveRecord(id);
            return records;
        }

        public void Restore(IEnumerable<TutorialSaveRecord> records)
        {
            completed.Clear();
            sessionCompleted.Clear();
            if (records == null) return;
            foreach (TutorialSaveRecord record in records)
            {
                if (record.TutorialId == LevelOneTutorialCompleteId)
                {
                    completed.Add(LevelOneTutorialCompleteId);
                }
            }
        }

        private static bool IsLevelOneTutorial(string id)
        {
            return id == "first_link_v2"
                || id == "second_wave_expansion_v1"
                || id == "fire_tower_v1";
        }
    }
}
