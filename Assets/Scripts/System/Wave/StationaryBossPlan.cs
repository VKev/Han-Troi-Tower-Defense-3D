using System;
using System.Collections.Generic;
using TowerDefense3D.Enemies;
using UnityEngine;

namespace TowerDefense3D.Waves
{
    /// <summary>
    /// One cast the standing boss performs during a wave: when it begins, and what walks out of it.
    /// </summary>
    [Serializable]
    public sealed class StationaryBossCast
    {
        [Tooltip("Seconds into the wave that the cast begins. The boss plays its skill and the "
            + "enemies below step out of it, the same way its summons work when it is fighting.")]
        [SerializeField, Min(0f)] private float castTimeSeconds;

        [SerializeField]
        private List<SummonerBossEnemyDefinition.SummonedEnemyEntry> summons =
            new List<SummonerBossEnemyDefinition.SummonedEnemyEntry>();

        public float CastTimeSeconds => castTimeSeconds;
        public IReadOnlyList<SummonerBossEnemyDefinition.SummonedEnemyEntry> Summons => summons;

        internal void CollectValidationErrors(ICollection<string> errors, string context)
        {
            if (castTimeSeconds < 0f)
            {
                errors.Add($"{context}: Cast Time Seconds cannot be negative.");
            }

            if (summons.Count == 0)
            {
                errors.Add($"{context}: A cast with no summons does nothing.");
                return;
            }

            for (int index = 0; index < summons.Count; index++)
            {
                SummonerBossEnemyDefinition.SummonedEnemyEntry entry = summons[index];
                if (entry == null || entry.Definition == null || entry.Count <= 0)
                {
                    errors.Add($"{context}: summon {index + 1} is invalid.");
                }
            }
        }
    }

    /// <summary>
    /// What the standing boss does during one wave.
    /// </summary>
    [Serializable]
    public sealed class StationaryBossWavePlan
    {
        [Tooltip("Which wave this applies to, counting from one.")]
        [SerializeField, Min(1)] private int waveNumber = 1;

        [SerializeField] private List<StationaryBossCast> casts = new List<StationaryBossCast>();

        public int WaveNumber => waveNumber;
        public IReadOnlyList<StationaryBossCast> Casts => casts;

        internal void CollectValidationErrors(
            ICollection<string> errors,
            string context,
            int waveCount,
            int firstStandingWaveNumber)
        {
            if (waveNumber < 1 || waveNumber > waveCount)
            {
                errors.Add($"{context}: Wave Number {waveNumber} is outside the schedule's {waveCount} waves.");
            }

            // A cast authored before the boss walks on would simply never happen, and nothing
            // would say so.
            if (waveNumber < firstStandingWaveNumber)
            {
                errors.Add(
                    $"{context}: the boss does not arrive until wave {firstStandingWaveNumber}, so "
                    + "it cannot cast here.");
            }

            // The boss fights on the last wave rather than standing on it, so a standing plan
            // there would describe something that never happens.
            if (waveNumber == waveCount)
            {
                errors.Add(
                    $"{context}: The last wave is the one the boss fights on, so it cannot carry a "
                    + "standing plan.");
            }

            if (casts.Count == 0)
            {
                errors.Add($"{context}: At least one cast is required, or remove the wave entry.");
                return;
            }

            for (int index = 0; index < casts.Count; index++)
            {
                StationaryBossCast cast = casts[index];
                if (cast == null)
                {
                    errors.Add($"{context}: cast {index + 1} is missing.");
                    continue;
                }

                cast.CollectValidationErrors(errors, $"{context}, cast {index + 1}");
            }
        }
    }

    /// <summary>
    /// The boss that stands on the road for most of a level, casting on a timer, and only joins
    /// the fight on the last wave.
    /// </summary>
    /// <remarks>
    /// It is described here rather than on the boss's own asset because this is per-level, per-wave
    /// direction: which wave, how far in, and what comes out. The asset keeps what the boss *is* -
    /// its health, its cast length, how far its summons stand from it - and that is shared by every
    /// level that uses it. Splitting them this way is also what puts the timing in the balance
    /// window's Waves tab, which is where someone tuning a level looks.
    ///
    /// While it stands, the boss cannot be hurt and does not hold the wave open. Both follow from
    /// the same decision: it is scenery with a schedule, not an opponent, until the last wave. That
    /// is also what keeps its health question from arising - each wave plans a fresh one, so it
    /// always arrives at full health and nothing carries over.
    /// </remarks>
    [Serializable]
    public sealed class StationaryBossPlan
    {
        [Tooltip("Leave empty for a level with no standing boss.")]
        [SerializeField] private SummonerBossEnemyDefinition boss;

        [Tooltip("The wave the boss walks on and takes up its position, counting from one. The "
            + "waves before it play out with no boss on the board at all.")]
        [SerializeField, Min(1)] private int firstStandingWaveNumber = 1;

        [Tooltip("How far along the road the boss stands, in metres from the start. Its summons "
            + "step out beside it, so this is also where they enter the road.")]
        [SerializeField, Min(0f)] private float standDistanceMeters = 12f;

        [Tooltip("Which authored Road Spawn the boss stands on. A level with several roads needs "
            + "this pinned: left automatic, the boss and the enemies it summons are handed roads "
            + "independently and the summons walk out onto a different one.")]
        [SerializeField, Min(0)] private int spawnPointIndex;

        [SerializeField]
        private List<StationaryBossWavePlan> waves = new List<StationaryBossWavePlan>();

        public SummonerBossEnemyDefinition Boss => boss;
        public int FirstStandingWaveNumber => firstStandingWaveNumber;
        public float StandDistanceMeters => standDistanceMeters;
        public int SpawnPointIndex => spawnPointIndex;
        public IReadOnlyList<StationaryBossWavePlan> Waves => waves;

        /// <summary>Whether this level has a boss standing on the road at all.</summary>
        public bool IsAuthored => boss != null;

        /// <summary>
        /// The casts for one wave, counting from one, or an empty list when the boss does nothing
        /// that wave.
        /// </summary>
        public IReadOnlyList<StationaryBossCast> GetCasts(int waveNumber)
        {
            for (int index = 0; index < waves.Count; index++)
            {
                StationaryBossWavePlan plan = waves[index];
                if (plan != null && plan.WaveNumber == waveNumber)
                {
                    return plan.Casts;
                }
            }

            return Array.Empty<StationaryBossCast>();
        }

        /// <summary>
        /// Whether the boss stands still, is untargetable, and is ignored by wave completion on
        /// this wave.
        /// </summary>
        /// <remarks>
        /// One question, asked in one place, because several systems need the same answer and
        /// several copies of it is how they would come to disagree.
        /// </remarks>
        public bool IsStandingOnWave(int waveNumber, int waveCount)
        {
            return IsAuthored
                && waveNumber >= firstStandingWaveNumber
                && waveNumber < waveCount;
        }

        /// <summary>Whether this is the wave the boss joins the fight on - the last one.</summary>
        /// <remarks>
        /// Asked in its own right rather than inferred as "not standing", because the waves before
        /// the boss arrives are not standing either. Reading those as fighting waves is exactly how
        /// the boss would end up walking into wave one.
        /// </remarks>
        public bool IsFightingOnWave(int waveNumber, int waveCount)
        {
            return IsAuthored && waveNumber == waveCount;
        }

        /// <summary>Whether the boss is on the board at all during this wave.</summary>
        public bool IsPresentOnWave(int waveNumber, int waveCount)
        {
            return IsStandingOnWave(waveNumber, waveCount)
                || IsFightingOnWave(waveNumber, waveCount);
        }

        internal void CollectValidationErrors(ICollection<string> errors, int waveCount)
        {
            if (!IsAuthored)
            {
                if (waves.Count > 0)
                {
                    errors.Add("Stationary Boss: waves are authored but no boss is assigned.");
                }

                return;
            }

            if (boss.Rank != EnemyRank.Boss)
            {
                errors.Add($"Stationary Boss: {boss.name} must use the Boss rank.");
            }

            if (firstStandingWaveNumber < 1 || firstStandingWaveNumber >= waveCount)
            {
                errors.Add(
                    $"Stationary Boss: First Standing Wave Number {firstStandingWaveNumber} must "
                    + $"be between 1 and {waveCount - 1} - the boss has to stand for at least one "
                    + "wave before the one it fights on.");
            }

            var seen = new List<int>();
            for (int index = 0; index < waves.Count; index++)
            {
                StationaryBossWavePlan plan = waves[index];
                if (plan == null)
                {
                    errors.Add($"Stationary Boss: wave entry {index + 1} is missing.");
                    continue;
                }

                if (seen.Contains(plan.WaveNumber))
                {
                    errors.Add(
                        $"Stationary Boss: wave {plan.WaveNumber} is described more than once.");
                }
                else
                {
                    seen.Add(plan.WaveNumber);
                }

                plan.CollectValidationErrors(
                    errors,
                    $"Stationary Boss, wave {plan.WaveNumber}",
                    waveCount,
                    firstStandingWaveNumber);
            }
        }
    }
}
