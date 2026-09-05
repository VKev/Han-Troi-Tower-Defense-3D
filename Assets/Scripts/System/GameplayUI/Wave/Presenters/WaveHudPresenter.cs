using System;
using System.Collections.Generic;
using TowerDefense3D.Enemies;
using TowerDefense3D.Waves;
using UnityEngine;

namespace TowerDefense3D.GameFlow
{
    public sealed class WaveHudPresenter
    {
        private readonly IWaveSystem waveSystem;
        private readonly IWaveHudView view;

        public WaveHudPresenter(IWaveSystem waveSystem, IWaveHudView view)
        {
            this.waveSystem = waveSystem ?? throw new ArgumentNullException(nameof(waveSystem));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
        }

        public void Connect()
        {
            view.Initialize();
            view.StartWaveRequested += HandleStartWaveRequested;
            view.Show();
        }

        public void Disconnect()
        {
            view.StartWaveRequested -= HandleStartWaveRequested;
        }

        public void Refresh()
        {
            WaveState state = waveSystem.CreateState();

            view.Render(new WaveHudState(
                CreateWaveCounterText(state),
                CreateStatusText(state),
                CreateWaveProgress(state),
                state.RemainingEnemyCount.ToString("00"),
                CreateStartWaveText(state),
                CreateStartWaveBonusText(state),
                CreatePreviewIcons(),
                state.CanStartWave));
        }

        private void HandleStartWaveRequested()
        {
            // The error is dropped on purpose: the HUD no longer carries a line to print it on.
            waveSystem.TryStartWave(out _);
            Refresh();
        }

        private static string CreateWaveCounterText(WaveState state)
        {
            return $"{state.CurrentWaveNumber:00} / {state.WaveCount:00}";
        }

        private static string CreateStatusText(WaveState state)
        {
            switch (state.Phase)
            {
                case WavePhase.Running:
                    return "WAVE IN PROGRESS";
                case WavePhase.Victory:
                    return "ALL WAVES CLEARED";
                case WavePhase.Defeat:
                    return "CÓC HAS FALLEN";
                default:
                    return state.CanStartWave
                        ? "READY TO START"
                        : "LINK A VALID CHAIN";
            }
        }

        private static float CreateWaveProgress(WaveState state)
        {
            if (state.Phase == WavePhase.Victory)
            {
                return 1f;
            }

            if (state.WaveCount <= 0)
            {
                return 0f;
            }

            return (float)(state.CurrentWaveNumber - 1) / state.WaveCount;
        }

        private static string CreateStartWaveText(WaveState state)
        {
            switch (state.Phase)
            {
                case WavePhase.Running:
                    return "WAVE RUNNING";
                case WavePhase.Victory:
                    return "VICTORY";
                case WavePhase.Defeat:
                    return "DEFEAT";
                default:
                    return "START WAVE";
            }
        }

        private static string CreateStartWaveBonusText(WaveState state)
        {
            return state.Phase == WavePhase.Preparation && state.NextWaveClearGold > 0
                ? $"+{state.NextWaveClearGold} CLEAR BONUS"
                : string.Empty;
        }

        /// <summary>
        /// One portrait per distinct enemy in the wave on show, in schedule order.
        /// </summary>
        /// <remarks>
        /// Distinct rather than one per batch: a wave that sends the same enemy in three waves of
        /// stragglers is still one kind of thing to brace for, and three copies of the same
        /// portrait would just crowd out the kinds the player has not seen yet.
        ///
        /// An enemy with no portrait assigned is skipped. That keeps a half-illustrated catalog
        /// previewing as fewer slots rather than as broken art, and it is why the grid is filled
        /// from a list instead of indexed by batch.
        ///
        /// Filled in every phase, including mid-wave. The grid belongs to the player now: they
        /// open and shut it when they like, so it must never be open over nothing.
        /// </remarks>
        private IReadOnlyList<Sprite> CreatePreviewIcons()
        {
            IReadOnlyList<EnemySpawnBatchDefinition> batches = waveSystem.GetNextWavePreview();
            var icons = new List<Sprite>(batches.Count);
            for (int index = 0; index < batches.Count; index++)
            {
                EnemyDefinition enemy = batches[index].Enemy;
                if (enemy == null || enemy.Icon == null || icons.Contains(enemy.Icon))
                {
                    continue;
                }

                icons.Add(enemy.Icon);
            }

            return icons;
        }
    }
}
