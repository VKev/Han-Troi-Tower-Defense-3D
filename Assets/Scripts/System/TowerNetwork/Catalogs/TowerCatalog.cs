using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.Towers
{
    [CreateAssetMenu(
        fileName = "TowerCatalog",
        menuName = "Tower Defense/Towers/Tower Catalog")]
    public sealed class TowerCatalog : ScriptableObject
    {
        [SerializeField] private TowerCombatRules combatRules;
        [SerializeField] private List<TowerCombatDefinition> definitions =
            new List<TowerCombatDefinition>();

        public TowerCombatRules CombatRules => combatRules;
        public IReadOnlyList<TowerCombatDefinition> Definitions => definitions;

        public bool TryGet(TowerFamily family, out TowerCombatDefinition definition)
        {
            for (int index = 0; index < definitions.Count; index++)
            {
                TowerCombatDefinition candidate = definitions[index];
                if (candidate != null && candidate.Family == family)
                {
                    definition = candidate;
                    return true;
                }
            }

            definition = null;
            return false;
        }
    }
}
