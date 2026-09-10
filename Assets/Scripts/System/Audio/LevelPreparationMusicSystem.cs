using System;
using TowerDefense3D.Waves;

namespace TowerDefense3D.Audio
{
    public sealed class LevelPreparationMusicSystem : IDisposable
    {
        private const float OutcomeWaveMusicVolume = 0.1f;
        private readonly ISoundPlayer soundPlayer;
        private readonly IWaveSystem waveSystem;
        private bool isStarted;
        private bool isPreparationPlaying;
        private bool isWavePlaying;
        private bool isOutcomeVisible;

        public LevelPreparationMusicSystem(ISoundPlayer soundPlayer, IWaveSystem waveSystem)
        {
            this.soundPlayer = soundPlayer ?? throw new ArgumentNullException(nameof(soundPlayer));
            this.waveSystem = waveSystem ?? throw new ArgumentNullException(nameof(waveSystem));
        }

        public void Start()
        {
            if (isStarted)
            {
                return;
            }

            isStarted = true;
            isOutcomeVisible = false;
            waveSystem.StateChanged += HandleWaveStateChanged;
            RefreshMusic();
        }

        public void Dispose()
        {
            if (!isStarted)
            {
                return;
            }

            isStarted = false;
            waveSystem.StateChanged -= HandleWaveStateChanged;
            soundPlayer.Stop(SoundId.LevelPreparationMusic);
            soundPlayer.Stop(SoundId.WaveMusic);
            isPreparationPlaying = false;
            isWavePlaying = false;
            isOutcomeVisible = false;
        }

        public void SetOutcomeVisible(bool visible)
        {
            isOutcomeVisible = visible;
            RefreshMusic();
        }

        private void HandleWaveStateChanged()
        {
            RefreshMusic();
        }

        private void RefreshMusic()
        {
            WavePhase phase = waveSystem.CreateState().Phase;
            if (waveSystem.IsRunning)
            {
                isOutcomeVisible = false;
                if (isPreparationPlaying)
                {
                    soundPlayer.Stop(SoundId.LevelPreparationMusic);
                    isPreparationPlaying = false;
                }

                if (!isWavePlaying)
                {
                    isWavePlaying = soundPlayer.Play(SoundId.WaveMusic);
                }

                soundPlayer.SetVolume(SoundId.WaveMusic, 1f);

                return;
            }

            if (phase == WavePhase.Victory || phase == WavePhase.Defeat)
            {
                if (isPreparationPlaying)
                {
                    soundPlayer.Stop(SoundId.LevelPreparationMusic);
                    isPreparationPlaying = false;
                }

                if (!isWavePlaying)
                {
                    isWavePlaying = soundPlayer.Play(SoundId.WaveMusic);
                }

                soundPlayer.SetVolume(
                    SoundId.WaveMusic,
                    isOutcomeVisible ? OutcomeWaveMusicVolume : 1f);
                return;
            }

            if (isWavePlaying)
            {
                soundPlayer.Stop(SoundId.WaveMusic);
                isWavePlaying = false;
            }

            if (phase == WavePhase.Preparation && !isPreparationPlaying)
            {
                isPreparationPlaying = soundPlayer.Play(SoundId.LevelPreparationMusic);
            }
        }
    }
}
