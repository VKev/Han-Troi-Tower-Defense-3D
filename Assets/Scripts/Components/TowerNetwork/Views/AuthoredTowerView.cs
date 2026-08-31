using UnityEngine;

namespace TowerDefense3D.Towers
{
    /// <summary>
    /// Marks a tower the level authored straight into its scene. The level scope adopts every
    /// one of these into the tower network once the level's systems are running, so a hero
    /// standing on the board before the first wave behaves like a tower the player built
    /// rather than like scenery.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TowerRuntimeView))]
    public sealed class AuthoredTowerView : MonoBehaviour
    {
        [SerializeField] private TowerCombatDefinition definition;

        public TowerCombatDefinition Definition => definition;

        public TowerRuntimeView RuntimeView => GetComponent<TowerRuntimeView>();

        /// <summary>
        /// Draws the hero's reach in the Scene view so its range can be tuned against the road
        /// without entering Play Mode. Combat does not consume the radius yet.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (!(definition is HeroTowerDefinition hero))
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.72f, 0.26f, 0.85f);
            Gizmos.DrawWireSphere(transform.position, hero.AttackRangeMeters);
        }
    }
}
