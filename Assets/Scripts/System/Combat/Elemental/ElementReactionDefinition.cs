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
        [SerializeField, Min(0f)] private float damage;
        [SerializeField, Min(0f)] private float radiusMeters;
        [SerializeField, Min(0f)] private float burnDamagePerTick;
        [SerializeField, Min(0f)] private float burnTickIntervalSeconds;
        [SerializeField, Min(0f)] private float burnDurationSeconds;
        [SerializeField, Min(0f)] private float liftDurationSeconds;

        [Tooltip("Seconds after a lift ends before the same enemy can be lifted again. "
            + "Must exceed the fastest re-application interval, otherwise a chain can "
            + "hold an enemy airborne forever and it never advances along the road.")]
        [SerializeField, Min(0f)] private float liftImmunitySeconds = 1.5f;

        public ElementReactionId ReactionId => reactionId;
        public string DisplayName => displayName;
        public ElementPair Pair => new ElementPair(firstElement, secondElement);
        public float Damage => damage;
        public float RadiusMeters => radiusMeters;
        public float BurnDamagePerTick => burnDamagePerTick;
        public float BurnTickIntervalSeconds => burnTickIntervalSeconds;
        public float BurnDurationSeconds => burnDurationSeconds;
        public float LiftDurationSeconds => liftDurationSeconds;
        public float LiftImmunitySeconds => liftImmunitySeconds;

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

            if (liftDurationSeconds > 0f && liftImmunitySeconds <= 0f)
            {
                errors.Add(
                    "Element Reaction Lift requires a positive Lift Immunity, "
                    + "otherwise an enemy can be held airborne indefinitely.");
            }

            return errors;
        }
    }
}
