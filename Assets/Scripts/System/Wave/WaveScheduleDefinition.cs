using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.Waves
{
    [CreateAssetMenu(
        fileName = "WaveSchedule",
        menuName = "Tower Defense/Waves/Wave Schedule")]
    public sealed class WaveScheduleDefinition : ScriptableObject
    {
        [SerializeField] private int randomSeed;
        [SerializeField] private List<WaveDefinition> waves = new List<WaveDefinition>();

        public int RandomSeed => randomSeed;
        public IReadOnlyList<WaveDefinition> Waves => waves;

        public IReadOnlyList<string> CollectValidationErrors()
        {
            var errors = new List<string>();
            if (waves.Count == 0)
            {
                errors.Add("Wave Schedule must contain at least one wave.");
                return errors;
            }

            for (int waveIndex = 0; waveIndex < waves.Count; waveIndex++)
            {
                waves[waveIndex].CollectValidationErrors(errors, waveIndex);
            }

            return errors;
        }
    }
}
