using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.Enemies
{
    public enum EnemyRank
    {
        Regular,
        MiniBoss,
        Boss
    }

    [CreateAssetMenu(
        fileName = "Enemy",
        menuName = "Tower Defense/Enemies/Enemy")]
    public class EnemyDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string stableId = "basic";
        [SerializeField] private string displayName = "Basic";
        [SerializeField] private EnemyRank rank;

        [Header("Presentation")]
        [SerializeField] private GameObject viewPrefab;

        [Header("Base Stats")]
        [SerializeField, Min(0.01f)] private float baseMaxHealth = 16f;
        [SerializeField, Min(0.01f)] private float baseMoveSpeed = 2f;
        [SerializeField, Min(0.01f)] private float baseHitRadius = 0.35f;

        [Header("Thermal Shield")]
        [SerializeField, Min(0)] private int thermalShockHitsToBreakShield = 2;

        [Header("Rewards")]
        [SerializeField, Min(0)] private int goldOnDeath = 10;
        [SerializeField, Min(0)] private int soulOnDirectHit = 1;

        [Header("Base Threat")]
        [SerializeField, Min(1)] private int leakDamage = 1;

        public string StableId => stableId;
        public string DisplayName => displayName;
        public EnemyRank Rank => rank;
        public GameObject ViewPrefab => viewPrefab;
        public float BaseMaxHealth => baseMaxHealth;
        public float BaseMoveSpeed => baseMoveSpeed;
        public float BaseHitRadius => baseHitRadius;
        public int ThermalShockHitsToBreakShield => thermalShockHitsToBreakShield;
        public int GoldOnDeath => goldOnDeath;
        public int SoulOnDirectHit => soulOnDirectHit;
        public int LeakDamage => leakDamage;

        internal void CollectValidationErrors(ICollection<string> errors)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                errors.Add($"{name}: Stable Id is required.");
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                errors.Add($"{name}: Display Name is required.");
            }

            if (viewPrefab == null)
            {
                errors.Add($"{name}: View Prefab is required.");
            }
            else
            {
                Animator animator = viewPrefab.GetComponent<Animator>();
                if (animator == null || animator.runtimeAnimatorController == null)
                {
                    errors.Add($"{name}: View Prefab requires an Animator with a Runtime Animator Controller.");
                }
            }

            if (baseMaxHealth <= 0f || baseMoveSpeed <= 0f || baseHitRadius <= 0f)
            {
                errors.Add($"{name}: Base Max Health, Base Move Speed and Base Hit Radius must be greater than zero.");
            }

            if (goldOnDeath < 0 || soulOnDirectHit < 0 || leakDamage <= 0)
            {
                errors.Add($"{name}: Rewards cannot be negative and Leak Damage must be positive.");
            }

            CollectSpecificValidationErrors(errors);
        }

        internal virtual void CollectSpecificValidationErrors(ICollection<string> errors)
        {
        }

    }
}
