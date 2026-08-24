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

        [Header("Base Stats")]
        [SerializeField, Min(0.01f)] private float baseMaxHealth = 16f;
        [SerializeField, Min(0.01f)] private float baseMoveSpeed = 2f;
        [SerializeField, Min(0f)] private float basePhysicalResistance;
        [SerializeField, Min(0f)] private float baseMagicResistance;

        [Header("Received Effect Multipliers")]
        [SerializeField, Range(0f, 1f)] private float elementStatusEffectMultiplier = 1f;
        [SerializeField, Range(0f, 1f)] private float slowStrengthMultiplier = 1f;
        [SerializeField, Range(0f, 1f)] private float slowDurationMultiplier = 1f;
        [SerializeField, Range(0f, 1f)] private float stunDurationMultiplier = 1f;
        [SerializeField, Range(0f, 1f)] private float levitateDurationMultiplier = 1f;
        [SerializeField, Range(0f, 1f)] private float pushDistanceMultiplier = 1f;

        [Header("Rewards")]
        [SerializeField, Min(0)] private int goldOnDeath = 10;
        [SerializeField, Min(0)] private int soulOnDirectHit = 1;

        public string StableId => stableId;
        public string DisplayName => displayName;
        public EnemyRank Rank => rank;
        public float BaseMaxHealth => baseMaxHealth;
        public float BaseMoveSpeed => baseMoveSpeed;
        public float BasePhysicalResistance => basePhysicalResistance;
        public float BaseMagicResistance => baseMagicResistance;
        public float ElementStatusEffectMultiplier => elementStatusEffectMultiplier;
        public float SlowStrengthMultiplier => slowStrengthMultiplier;
        public float SlowDurationMultiplier => slowDurationMultiplier;
        public float StunDurationMultiplier => stunDurationMultiplier;
        public float LevitateDurationMultiplier => levitateDurationMultiplier;
        public float PushDistanceMultiplier => pushDistanceMultiplier;
        public int GoldOnDeath => goldOnDeath;
        public int SoulOnDirectHit => soulOnDirectHit;

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

            if (baseMaxHealth <= 0f || baseMoveSpeed <= 0f)
            {
                errors.Add($"{name}: Base Max Health and Base Move Speed must be greater than zero.");
            }

            if (basePhysicalResistance < 0f || baseMagicResistance < 0f)
            {
                errors.Add($"{name}: Base resistances cannot be negative.");
            }

            if (!IsUnitMultiplier(elementStatusEffectMultiplier) ||
                !IsUnitMultiplier(slowStrengthMultiplier) ||
                !IsUnitMultiplier(slowDurationMultiplier) ||
                !IsUnitMultiplier(stunDurationMultiplier) ||
                !IsUnitMultiplier(levitateDurationMultiplier) ||
                !IsUnitMultiplier(pushDistanceMultiplier))
            {
                errors.Add($"{name}: Received effect multipliers must be between zero and one.");
            }

            if (goldOnDeath < 0 || soulOnDirectHit < 0)
            {
                errors.Add($"{name}: Rewards cannot be negative.");
            }

            CollectSpecificValidationErrors(errors);
        }

        internal virtual void CollectSpecificValidationErrors(ICollection<string> errors)
        {
        }

        private static bool IsUnitMultiplier(float value)
        {
            return value >= 0f && value <= 1f;
        }
    }
}
