using System;
using TowerDefense3D.Waves;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Drives the development shortcut that declares the level won. It exists so the victory
    /// sequence - the Cóc hopping off the board, then the outcome panel - can be watched without
    /// playing every wave.
    ///
    /// The cheat only reports the waves beaten. Everything downstream, including the escape and
    /// the panel, is left to the normal victory flow, so what the cheat shows is what a real clear
    /// shows.
    /// </summary>
    public sealed class LevelSkipCheatPresenter
    {
        private readonly IWaveSystem waveSystem;
        private readonly ILevelSkipCheatView view;

        public LevelSkipCheatPresenter(IWaveSystem waveSystem, ILevelSkipCheatView view)
        {
            this.waveSystem = waveSystem ?? throw new ArgumentNullException(nameof(waveSystem));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
        }

        public void Connect()
        {
            view.Initialize();
            view.SkipToVictoryRequested += HandleSkipToVictoryRequested;
            view.Show();
            Refresh();
        }

        public void Disconnect()
        {
            view.SkipToVictoryRequested -= HandleSkipToVictoryRequested;
            view.Shutdown();
        }

        public void Refresh()
        {
            view.Render(!IsLevelOver);
        }

        private bool IsLevelOver
        {
            get
            {
                WavePhase phase = waveSystem.CreateState().Phase;
                return phase == WavePhase.Victory || phase == WavePhase.Defeat;
            }
        }

        private void HandleSkipToVictoryRequested()
        {
            if (IsLevelOver)
            {
                return;
            }

            waveSystem.ForceVictory();
            Refresh();
        }
    }
}
