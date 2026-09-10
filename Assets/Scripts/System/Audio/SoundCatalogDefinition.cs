using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.Audio
{
    [CreateAssetMenu(fileName = "SoundCatalog", menuName = "Tower Defense/Audio/Sound Catalog")]
    public sealed class SoundCatalogDefinition : ScriptableObject
    {
        [SerializeField] private List<SoundDefinition> sounds = new List<SoundDefinition>();

        public bool TryGet(SoundId id, out SoundDefinition definition)
        {
            for (int index = 0; index < sounds.Count; index++)
            {
                SoundDefinition candidate = sounds[index];
                if (candidate != null && candidate.Id == id)
                {
                    definition = candidate;
                    return true;
                }
            }

            definition = null;
            return false;
        }

        public IReadOnlyList<string> CollectValidationErrors()
        {
            var errors = new List<string>();
            var ids = new HashSet<SoundId>();
            for (int index = 0; index < sounds.Count; index++)
            {
                SoundDefinition definition = sounds[index];
                if (definition == null)
                {
                    errors.Add($"Sound entry {index} is missing.");
                    continue;
                }

                if (definition.Id == SoundId.None)
                {
                    errors.Add($"Sound entry {index} requires a Sound Id.");
                }
                else if (!ids.Add(definition.Id))
                {
                    errors.Add($"Sound Id '{definition.Id}' is authored more than once.");
                }

                if (definition.Clip == null)
                {
                    errors.Add($"Sound Id '{definition.Id}' requires an Audio Clip.");
                }

                if (definition.MaxConcurrent <= 0)
                {
                    errors.Add($"Sound Id '{definition.Id}' requires a positive Max Concurrent value.");
                }
            }

            return errors;
        }
    }

    [Serializable]
    public sealed class SoundDefinition
    {
        [SerializeField] private SoundId id;
        [SerializeField] private AudioClip clip;
        [SerializeField, Range(0f, 1f)] private float volume = 1f;
        [SerializeField, InspectorName("Playback Speed"), Range(0.5f, 2f)]
        [Tooltip("Changes playback speed and pitch together. 1 is normal speed.")]
        private float pitch = 1f;
        [SerializeField, Min(1)] private int maxConcurrent = 1;
        [SerializeField, Min(0f)] private float cooldownSeconds;
        [SerializeField, Min(0)] private int priority = 10;
        [SerializeField] private bool isProtected;
        [SerializeField] private bool loop;

        public SoundId Id => id;
        public AudioClip Clip => clip;
        public float Volume => volume;
        public float Pitch => pitch;
        public int MaxConcurrent => maxConcurrent;
        public float CooldownSeconds => cooldownSeconds;
        public int Priority => priority;
        public bool IsProtected => isProtected;
        public bool Loop => loop;
    }
}
