using System.Collections.Generic;
using TowerDefense3D.Audio;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TowerDefense3D.Audio
{
    [DisallowMultipleComponent]
    public sealed class AudioPlaybackView : MonoBehaviour, ISoundPlayer
    {
        private readonly Dictionary<SoundId, float> lastPlayTimes = new Dictionary<SoundId, float>();
        [SerializeField] private AudioListener audioListener;
        [SerializeField] private AudioSource[] voices;

        private SoundId[] activeIds;
        private int[] activePriorities;
        private bool[] protectedVoices;
        private float[] startedAt;
        private SoundCatalogDefinition catalog;

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        public void Initialize(SoundCatalogDefinition soundCatalog)
        {
            catalog = soundCatalog;
            if (audioListener == null || voices == null || voices.Length == 0)
            {
                throw new MissingReferenceException(
                    "AudioPlaybackView requires an authored AudioListener and AudioSource voices.");
            }

            EnsureSingleAudioListener();
            if (activeIds != null)
            {
                return;
            }

            activeIds = new SoundId[voices.Length];
            activePriorities = new int[voices.Length];
            protectedVoices = new bool[voices.Length];
            startedAt = new float[voices.Length];
            for (int index = 0; index < voices.Length; index++)
            {
                AudioSource voice = voices[index];
                if (voice == null)
                {
                    throw new MissingReferenceException($"Audio voice {index + 1} is not authored.");
                }

                voice.playOnAwake = false;
                voice.spatialBlend = 0f;
                voice.dopplerLevel = 0f;
                voice.reverbZoneMix = 0f;
            }
        }

        public bool Play(SoundId id)
        {
            if (catalog == null || id == SoundId.None || !catalog.TryGet(id, out SoundDefinition definition)
                || definition.Clip == null)
            {
                return false;
            }

            float now = Time.unscaledTime;
            ReleaseCompletedVoices();
            if (lastPlayTimes.TryGetValue(id, out float lastPlayTime)
                && now - lastPlayTime < definition.CooldownSeconds)
            {
                return false;
            }

            if (CountPlaying(id) >= definition.MaxConcurrent || !TryGetVoice(definition, out int voiceIndex))
            {
                return false;
            }

            AudioSource voice = voices[voiceIndex];
            if (voice.isPlaying)
            {
                ResetVoice(voiceIndex, true);
            }

            voice.clip = definition.Clip;
            voice.volume = definition.Volume;
            voice.pitch = definition.Pitch;
            voice.loop = definition.Loop;
            voice.Play();
            activeIds[voiceIndex] = id;
            activePriorities[voiceIndex] = definition.Priority;
            protectedVoices[voiceIndex] = definition.IsProtected;
            startedAt[voiceIndex] = now;
            lastPlayTimes[id] = now;
            return true;
        }

        public void Stop(SoundId id)
        {
            if (voices == null || id == SoundId.None)
            {
                return;
            }

            for (int index = 0; index < activeIds.Length; index++)
            {
                if (activeIds[index] == id)
                {
                    ResetVoice(index, true);
                }
            }
        }

        public void SetVolume(SoundId id, float normalizedVolume)
        {
            if (voices == null || catalog == null || id == SoundId.None
                || !catalog.TryGet(id, out SoundDefinition definition))
            {
                return;
            }

            float volume = definition.Volume * Mathf.Clamp01(normalizedVolume);
            for (int index = 0; index < activeIds.Length; index++)
            {
                if (activeIds[index] == id && voices[index] != null)
                {
                    voices[index].volume = volume;
                }
            }
        }

        private void ReleaseCompletedVoices()
        {
            for (int index = 0; index < voices.Length; index++)
            {
                if (voices[index] == null || !voices[index].isPlaying)
                {
                    ResetVoice(index, false);
                }
            }
        }

        private void ResetVoice(int voiceIndex, bool stop)
        {
            // Teardown destroys the voices in no fixed order relative to the systems that still
            // hold this player, so a stop arriving after they are gone clears bookkeeping only.
            AudioSource voice = voices[voiceIndex];
            if (voice != null)
            {
                if (stop)
                {
                    voice.Stop();
                }

                voice.clip = null;
                voice.loop = false;
                voice.volume = 1f;
                voice.pitch = 1f;
            }

            activeIds[voiceIndex] = SoundId.None;
            activePriorities[voiceIndex] = 0;
            protectedVoices[voiceIndex] = false;
            startedAt[voiceIndex] = 0f;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _ = scene;
            _ = mode;
            EnsureSingleAudioListener();
        }

        private void EnsureSingleAudioListener()
        {
            if (audioListener == null)
            {
                return;
            }

            audioListener.enabled = true;
            AudioListener[] listeners = FindObjectsByType<AudioListener>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int index = 0; index < listeners.Length; index++)
            {
                AudioListener candidate = listeners[index];
                if (candidate != audioListener)
                {
                    candidate.enabled = false;
                }
            }
        }

        private int CountPlaying(SoundId id)
        {
            int count = 0;
            for (int index = 0; index < activeIds.Length; index++)
            {
                if (activeIds[index] == id)
                {
                    count++;
                }
            }

            return count;
        }

        private bool TryGetVoice(SoundDefinition definition, out int voiceIndex)
        {
            voiceIndex = -1;
            for (int index = 0; index < voices.Length; index++)
            {
                if (voices[index] == null)
                {
                    continue;
                }

                if (!voices[index].isPlaying)
                {
                    voiceIndex = index;
                    return true;
                }
            }

            for (int index = 0; index < voices.Length; index++)
            {
                if (voices[index] == null
                    || protectedVoices[index]
                    || activePriorities[index] >= definition.Priority)
                {
                    continue;
                }

                if (voiceIndex < 0 || activePriorities[index] < activePriorities[voiceIndex]
                    || (activePriorities[index] == activePriorities[voiceIndex]
                        && startedAt[index] < startedAt[voiceIndex]))
                {
                    voiceIndex = index;
                }
            }

            return voiceIndex >= 0;
        }
    }
}
