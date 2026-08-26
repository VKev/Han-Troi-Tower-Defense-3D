using System.Collections.Generic;
using TowerDefense3D.Towers;
using UnityEngine;

namespace TowerDefense3D.Enemies
{
    [CreateAssetMenu(
        fileName = "ElementReaction",
        menuName = "Tower Defense/Combat/Element Reaction")]
    public sealed class ElementReactionDefinition : ScriptableObject
    {
        [SerializeField] private ElementReactionId reactionId;
        [SerializeField] private string displayName;
        [SerializeField] private ElementType firstElement;
        [SerializeField] private ElementType secondElement;
        [SerializeField, Min(0f)] private float physicalDamage;
        [SerializeField, Min(0f)] private float magicDamage;
        [SerializeField, Min(0f)] private float radiusMeters;
        [SerializeField, Min(0f)] private float burnDamagePerTick;
        [SerializeField, Min(0f)] private float burnTickIntervalSeconds;
        [SerializeField, Min(0f)] private float burnDurationSeconds;
        [SerializeField, Range(0f, 1f)] private float slowStrengthFraction;
        [SerializeField, Min(0f)] private float slowDurationSeconds;
        [SerializeField, Min(0f)] private float pushDistanceMeters;
        [SerializeField, Min(0f)] private float physicalResistanceReductionPoints;
        [SerializeField, Min(0f)] private float magicResistanceReductionPoints;
        [SerializeField, Min(0f)] private float resistanceReductionDurationSeconds;
        [SerializeField] private bool createsField;

        public ElementReactionId ReactionId => reactionId;
        public string DisplayName => displayName;
        public ElementPair Pair => new ElementPair(firstElement, secondElement);
        public float PhysicalDamage => physicalDamage;
        public float MagicDamage => magicDamage;
        public float RadiusMeters => radiusMeters;
        public float BurnDamagePerTick => burnDamagePerTick;
        public float BurnTickIntervalSeconds => burnTickIntervalSeconds;
        public float BurnDurationSeconds => burnDurationSeconds;
        public float SlowStrengthFraction => slowStrengthFraction;
        public float SlowDurationSeconds => slowDurationSeconds;
        public float PushDistanceMeters => pushDistanceMeters;
        public float PhysicalResistanceReductionPoints => physicalResistanceReductionPoints;
        public float MagicResistanceReductionPoints => magicResistanceReductionPoints;
        public float ResistanceReductionDurationSeconds => resistanceReductionDurationSeconds;
        public bool CreatesField => createsField;

        public IReadOnlyList<string> CollectValidationErrors()
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(displayName))
            {
                errors.Add("Element Reaction requires a display name.");
            }

            if (burnDamagePerTick > 0f &&
                (burnTickIntervalSeconds <= 0f || burnDurationSeconds <= 0f))
            {
                errors.Add("Element Reaction Burn requires a positive interval and duration.");
            }

            if (slowStrengthFraction > 0f && slowDurationSeconds <= 0f)
            {
                errors.Add("Element Reaction Slow requires a positive duration.");
            }

            if ((physicalResistanceReductionPoints > 0f || magicResistanceReductionPoints > 0f)
                && resistanceReductionDurationSeconds <= 0f)
            {
                errors.Add("Element Reaction Resistance reduction requires a positive duration.");
            }

            if (createsField && radiusMeters <= 0f)
            {
                errors.Add("Element Reaction Field requires a positive radius.");
            }

            if (reactionId == ElementReactionId.PureRewrite && HasSpecialEffect)
            {
                errors.Add("Pure Rewrite cannot deal damage or apply a special effect.");
            }

            return errors;
        }

        private bool HasSpecialEffect => physicalDamage > 0f || magicDamage > 0f || radiusMeters > 0f
            || burnDamagePerTick > 0f || slowStrengthFraction > 0f || pushDistanceMeters > 0f
            || physicalResistanceReductionPoints > 0f || magicResistanceReductionPoints > 0f
            || createsField;
    }
}
