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
            if (!waveSystem.TryStartWave(out string error))
            {
                // Discarding this used to make a refused wave indistinguishable from a dead
                // button: no message, no log, nothing in the console to search for. The reason is
                // always worth having even before there is somewhere on screen to print it.
                Debug.LogWarning("Wave refused: " + error);
            }
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
                    return "ĐANG DIỄN RA";
                case WavePhase.Victory:
                    return "ĐÃ QUA TẤT CẢ ĐỢT";
                case WavePhase.Defeat:
                    return "CÓC ĐÃ GỤC";
                default:
                    return state.CanStartWave
                        ? "SẴN SÀNG BẮT ĐẦU"
                        : "HÃY NỐI CHUỖI HỢP LỆ";
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
                    return "ĐANG CHẠY";
                case WavePhase.Victory:
                    return "CHIẾN THẮNG";
                case WavePhase.Defeat:
                    return "THẤT BẠI";
                default:
                    return "BẮT ĐẦU ĐỢT";
            }
        }

        private static string CreateStartWaveBonusText(WaveState state)
        {
            return state.Phase == WavePhase.Preparation && state.NextWaveClearGold > 0
                ? $"+{state.NextWaveClearGold} THƯỞNG VƯỢT ĐỢT"
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
