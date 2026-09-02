using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.Towers
{
    /// <summary>
    /// A special "Hero" tower. It is built through the ordinary placement flow, but stays
    /// locked in the build bar until the player clears its authored level, and it strikes
    /// on its own inside <see cref="AttackRangeMeters"/> instead of relaying an upstream
    /// projectile. It is modelled as a source so it owns no input queue.
    /// </summary>
    [CreateAssetMenu(
        fileName = "HeroTower",
        menuName = "Tower Defense/Towers/Heroes/Hero")]
    public sealed class HeroTowerDefinition : TowerCombatDefinition
    {
        [SerializeField] private TowerCoreProfile core = new TowerCoreProfile(
            "hero_crab",
            "Hero",
            new TowerNetworkProfile(0, 1, 0, false),
            new TowerThroughputProfile(1.2f, 1, 1),
            new TowerEconomyProfile(150, 0, 1, true));
        [Tooltip("How far, in meters, the hero reaches from its own position.")]
        [SerializeField, Min(0.01f)] private float attackRangeMeters = 4f;
        [SerializeField] private DamageProfile attackDamage = new DamageProfile(14f);
        [SerializeField, Min(0f)] private float attackAoeRadiusMeters = 2f;
        [SerializeField, Min(0.01f)] private float prepareDurationSeconds = 1f;
        [SerializeField, Range(0f, 1f)] private float lungeFractionOfAttackInterval = 5f / 24f;
        [SerializeField, Range(0f, 1f)] private float impactHoldFractionOfAttackInterval = 2f / 24f;
        [SerializeField, Range(0f, 1f)] private float returnFractionOfAttackInterval = 5f / 24f;
        [Tooltip("Level the player has to clear before this hero unlocks. Zero never locks it.")]
        [SerializeField, Min(0)] private int unlockAfterClearingLevel = 7;

        public override TowerFamily Family => TowerFamily.Hero;
        public override TowerNetworkRole NetworkRole => TowerNetworkRole.Source;
        public override TowerCoreProfile Core => core;
        public override int UnlockAfterClearingLevelNumber => unlockAfterClearingLevel;
        public float AttackRangeMeters => attackRangeMeters;
        public DamageProfile AttackDamage => attackDamage;
        public float AttackAoeRadiusMeters => attackAoeRadiusMeters;
        public float PrepareDurationSeconds => prepareDurationSeconds;
        public float LungeDurationSeconds => Core.Throughput.CycleIntervalSeconds * lungeFractionOfAttackInterval;
        public float ImpactHoldDurationSeconds => Core.Throughput.CycleIntervalSeconds * impactHoldFractionOfAttackInterval;
        public float ReturnDurationSeconds => Core.Throughput.CycleIntervalSeconds * returnFractionOfAttackInterval;

        internal override void CollectSpecificValidationErrors(List<string> errors)
        {
            if (attackRangeMeters <= 0f)
            {
                errors.Add("Hero Attack Range must be greater than zero.");
            }

            if (attackDamage == null || attackDamage.Amount <= 0f)
            {
                errors.Add("Hero Attack Damage must be greater than zero.");
            }

            if (prepareDurationSeconds <= 0f)
            {
                errors.Add("Hero Prepare duration must be greater than zero.");
            }

            float presentationSeconds = prepareDurationSeconds + LungeDurationSeconds
                + ImpactHoldDurationSeconds + ReturnDurationSeconds;
            if (presentationSeconds > Core.Throughput.CycleIntervalSeconds + 0.0001f)
            {
                errors.Add("Hero Prepare, lunge, impact, and return must fit inside the attack interval.");
            }
        }
    }
}
