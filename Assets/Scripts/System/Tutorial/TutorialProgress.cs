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
        private readonly HashSet<string> completed = new HashSet<string>(StringComparer.Ordinal);

        public bool IsCompleted(string id) => !string.IsNullOrEmpty(id) && completed.Contains(id);

        public void MarkCompleted(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                completed.Add(id);
            }
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
            if (records == null) return;
            foreach (TutorialSaveRecord record in records) MarkCompleted(record.TutorialId);
        }
    }
}
