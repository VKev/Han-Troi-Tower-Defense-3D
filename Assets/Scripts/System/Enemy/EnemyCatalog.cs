using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.Enemies
{
    [CreateAssetMenu(
        fileName = "EnemyCatalog",
        menuName = "Tower Defense/Enemies/Enemy Catalog")]
    public sealed class EnemyCatalog : ScriptableObject
    {
        [SerializeField] private List<EnemyDefinition> definitions = new List<EnemyDefinition>();

        public IReadOnlyList<EnemyDefinition> Definitions => definitions;

        public bool TryGet(string stableId, out EnemyDefinition definition)
        {
            for (int index = 0; index < definitions.Count; index++)
            {
                EnemyDefinition candidate = definitions[index];
                if (candidate != null && string.Equals(candidate.StableId, stableId, StringComparison.Ordinal))
                {
                    definition = candidate;
                    return true;
                }
            }

            definition = null;
            return false;
        }

        public IReadOnlyList<string> CollectValidationErrors()
        {
            var errors = new List<string>();
            var stableIds = new HashSet<string>(StringComparer.Ordinal);

            for (int index = 0; index < definitions.Count; index++)
            {
                EnemyDefinition definition = definitions[index];
                if (definition == null)
                {
                    errors.Add($"Enemy Catalog entry {index} is missing.");
                    continue;
                }

                definition.CollectValidationErrors(errors);
                if (!string.IsNullOrWhiteSpace(definition.StableId) && !stableIds.Add(definition.StableId))
                {
                    errors.Add($"Duplicate Enemy Stable Id '{definition.StableId}'.");
                }
            }

            return errors;
        }
    }
}
