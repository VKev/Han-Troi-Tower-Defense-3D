using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.Enemies
{
    [CreateAssetMenu(
        fileName = "SummonerBossEnemy",
        menuName = "Tower Defense/Enemies/Summoner Boss Enemy")]
    public sealed class SummonerBossEnemyDefinition : EnemyDefinition
    {
        [Serializable]
        public sealed class SummonedEnemyEntry
        {
            [SerializeField] private EnemyDefinition definition;
            [SerializeField, Min(1)] private int count = 1;

            public EnemyDefinition Definition => definition;
            public int Count => count;
        }

        [Serializable]
        public sealed class SummonPhase
        {
            [SerializeField, Range(0f, 1f)] private float startHealthFraction = 1f;
            [SerializeField, Min(0.01f)] private float summonIntervalSeconds = 8f;
            [SerializeField] private List<SummonedEnemyEntry> entries = new List<SummonedEnemyEntry>();

            public float StartHealthFraction => startHealthFraction;
            public float SummonIntervalSeconds => summonIntervalSeconds;
            public IReadOnlyList<SummonedEnemyEntry> Entries => entries;
        }

        [SerializeField] private List<SummonPhase> summonPhases = new List<SummonPhase>();
        [SerializeField, Min(0.01f)] private float summonSkillDurationSeconds = 3f;

        public IReadOnlyList<SummonPhase> SummonPhases => summonPhases;
        public float SummonSkillDurationSeconds => summonSkillDurationSeconds;

        internal override void CollectSpecificValidationErrors(ICollection<string> errors)
        {
            if (Rank != EnemyRank.Boss)
            {
                errors.Add($"{name}: A Summoner Boss must use the Boss rank.");
            }

            if (summonPhases.Count == 0)
            {
                errors.Add($"{name}: At least one Summon Phase is required.");
                return;
            }

            float previousThreshold = 2f;
            for (int phaseIndex = 0; phaseIndex < summonPhases.Count; phaseIndex++)
            {
                SummonPhase phase = summonPhases[phaseIndex];
                if (phase == null)
                {
                    errors.Add($"{name}: Summon Phase {phaseIndex} is missing.");
                    continue;
                }

                if (phaseIndex == 0 && !Mathf.Approximately(phase.StartHealthFraction, 1f))
                {
                    errors.Add($"{name}: The first Summon Phase must start at full health.");
                }

                if (phase.StartHealthFraction <= 0f ||
                    phase.StartHealthFraction > 1f ||
                    phase.StartHealthFraction >= previousThreshold)
                {
                    errors.Add($"{name}: Summon Phase thresholds must be positive and strictly descending.");
                }

                if (phase.SummonIntervalSeconds <= 0f)
                {
                    errors.Add($"{name}: Summon Phase {phaseIndex} requires a positive interval.");
                }

                if (phase.Entries.Count == 0)
                {
                    errors.Add($"{name}: Summon Phase {phaseIndex} requires at least one Enemy entry.");
                }

                for (int entryIndex = 0; entryIndex < phase.Entries.Count; entryIndex++)
                {
                    SummonedEnemyEntry entry = phase.Entries[entryIndex];
                    if (entry == null || entry.Definition == null || entry.Count <= 0)
                    {
                        errors.Add($"{name}: Summon Phase {phaseIndex}, entry {entryIndex} is invalid.");
                        continue;
                    }

                    if (entry.Definition == this)
                    {
                        errors.Add($"{name}: A Summoner Boss cannot summon itself.");
                    }
                }

                previousThreshold = phase.StartHealthFraction;
            }
        }
    }
}
