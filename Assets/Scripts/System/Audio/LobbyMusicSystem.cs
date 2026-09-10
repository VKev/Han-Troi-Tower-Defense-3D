using System;
using TowerDefense3D.GameFlow;

namespace TowerDefense3D.Audio
{
    public sealed class LobbyMusicSystem : IDisposable
    {
        private readonly GameFlowSystem gameFlowSystem;
        private readonly ISoundPlayer soundPlayer;
        private bool isStarted;
        private bool isMusicPlaying;

        public LobbyMusicSystem(GameFlowSystem gameFlowSystem, ISoundPlayer soundPlayer)
        {
            this.gameFlowSystem = gameFlowSystem ?? throw new ArgumentNullException(nameof(gameFlowSystem));
            this.soundPlayer = soundPlayer ?? throw new ArgumentNullException(nameof(soundPlayer));
        }

        public void Start()
        {
            if (isStarted)
            {
                return;
            }

            isStarted = true;
            gameFlowSystem.StateChanged += HandleStateChanged;
            HandleStateChanged(gameFlowSystem.State);
        }

        public void Dispose()
        {
            if (!isStarted)
            {
                return;
            }

            isStarted = false;
            gameFlowSystem.StateChanged -= HandleStateChanged;
            StopMusic();
        }

        private void HandleStateChanged(GameFlowState state)
        {
            if (state == GameFlowState.LoadingLevel || state == GameFlowState.Gameplay)
            {
                StopMusic();
                return;
            }

            if (!isMusicPlaying)
            {
                isMusicPlaying = soundPlayer.Play(SoundId.LobbyMusic);
            }
        }

        private void StopMusic()
        {
            if (!isMusicPlaying)
            {
                return;
            }

            soundPlayer.Stop(SoundId.LobbyMusic);
            isMusicPlaying = false;
        }
    }
}
