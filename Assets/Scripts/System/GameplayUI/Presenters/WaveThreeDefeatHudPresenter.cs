using System;
using TowerDefense3D.Waves;

namespace TowerDefense3D.GameFlow
{
    public sealed class WaveThreeDefeatHudPresenter
    {
        private readonly IWaveSystem waveSystem;
        private readonly IWaveThreeDefeatHudView view;

        public WaveThreeDefeatHudPresenter(IWaveSystem waveSystem, IWaveThreeDefeatHudView view)
        {
            this.waveSystem = waveSystem ?? throw new ArgumentNullException(nameof(waveSystem));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
        }

        public void Connect()
        {
            waveSystem.StateChanged += Refresh;
            view.RetryWaveThreeRequested += HandleRetryRequested;
            Refresh();
        }

        public void Disconnect()
        {
            waveSystem.StateChanged -= Refresh;
            view.RetryWaveThreeRequested -= HandleRetryRequested;
            view.HideWaveThreeDefeat();
        }

        public void Refresh()
        {
            if (waveSystem.IsCurrentWaveRetryAvailable)
            {
                view.ShowWaveThreeDefeat();
            }
            else
            {
                view.HideWaveThreeDefeat();
            }
        }

        private void HandleRetryRequested()
        {
            view.HideWaveThreeDefeat();
            waveSystem.RetryCurrentWave();
        }
    }
}
