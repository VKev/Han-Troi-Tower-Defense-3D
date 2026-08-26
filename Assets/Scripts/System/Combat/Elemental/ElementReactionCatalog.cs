using System;
using System.Collections.Generic;
using TowerDefense3D.Towers;
using UnityEngine;

namespace TowerDefense3D.Enemies
{
    [CreateAssetMenu(
        fileName = "ElementReactionCatalog",
        menuName = "Tower Defense/Combat/Element Reaction Catalog")]
    public sealed class ElementReactionCatalog : ScriptableObject
    {
        private const int ExpectedPairCount = 10;

        [SerializeField, Min(0.01f)] private float elementMarkDurationSeconds = 3f;
        [SerializeField, Min(0.01f)] private float reactionCooldownSeconds = 0.2f;
        [SerializeField, Range(0f, 1f)] private float maximumSlowFraction = 0.7f;
        [SerializeField] private List<ElementReactionDefinition> definitions =
            new List<ElementReactionDefinition>();

        public float ElementMarkDurationSeconds => elementMarkDurationSeconds;
        public float ReactionCooldownSeconds => reactionCooldownSeconds;
        public float MaximumSlowFraction => maximumSlowFraction;
        public IReadOnlyList<ElementReactionDefinition> Definitions => definitions;

        public ElementReactionDefinition Get(ElementType first, ElementType second)
        {
            ElementPair pair = new ElementPair(first, second);
            for (int index = 0; index < definitions.Count; index++)
            {
                ElementReactionDefinition definition = definitions[index];
                if (definition.Pair.Equals(pair))
                {
                    return definition;
                }
            }

            throw new InvalidOperationException($"Element Reaction pair '{pair}' is not authored.");
        }

        public IReadOnlyList<string> CollectValidationErrors()
        {
            var errors = new List<string>();
            if (elementMarkDurationSeconds <= 0f)
            {
                errors.Add("Element mark duration must be positive.");
            }

            if (reactionCooldownSeconds <= 0f)
            {
                errors.Add("Reaction cooldown must be positive.");
            }

            if (maximumSlowFraction <= 0f || maximumSlowFraction > 0.7f)
            {
                errors.Add("Maximum Slow must be greater than zero and no higher than 70%.");
            }

            var authoredPairs = new HashSet<ElementPair>();
            for (int index = 0; index < definitions.Count; index++)
            {
                ElementReactionDefinition definition = definitions[index];
                if (definition == null)
                {
                    errors.Add($"Element Reaction entry {index} is missing.");
                    continue;
                }

                if (!authoredPairs.Add(definition.Pair))
                {
                    errors.Add($"Element Reaction pair '{definition.Pair}' is authored more than once.");
                }

                IReadOnlyList<string> definitionErrors = definition.CollectValidationErrors();
                for (int errorIndex = 0; errorIndex < definitionErrors.Count; errorIndex++)
                {
                    errors.Add($"{definition.name}: {definitionErrors[errorIndex]}");
                }
            }

            if (authoredPairs.Count != ExpectedPairCount)
            {
                errors.Add($"Element Reaction Catalog requires all {ExpectedPairCount} unordered pairs.");
            }

            return errors;
        }
    }
}
