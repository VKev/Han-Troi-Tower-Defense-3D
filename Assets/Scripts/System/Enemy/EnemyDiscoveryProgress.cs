using System;
using System.Collections.Generic;

namespace TowerDefense3D.Enemies
{
    public sealed class EnemyDiscoveryProgress
    {
        private readonly HashSet<string> discoveredIds = new HashSet<string>(StringComparer.Ordinal);

        public event Action Changed;

        public bool IsDiscovered(string stableId)
        {
            return !string.IsNullOrWhiteSpace(stableId) && discoveredIds.Contains(stableId);
        }

        public void MarkDiscovered(string stableId)
        {
            if (!string.IsNullOrWhiteSpace(stableId) && discoveredIds.Add(stableId))
            {
                Changed?.Invoke();
            }
        }

        public string[] CreateSnapshot()
        {
            var snapshot = new string[discoveredIds.Count];
            discoveredIds.CopyTo(snapshot);
            Array.Sort(snapshot, StringComparer.Ordinal);
            return snapshot;
        }

        public void Restore(IEnumerable<string> stableIds)
        {
            discoveredIds.Clear();
            if (stableIds == null) return;
            foreach (string stableId in stableIds)
            {
                if (!string.IsNullOrWhiteSpace(stableId)) discoveredIds.Add(stableId);
            }
        }
    }
}
