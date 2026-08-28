using System;
using System.Collections.Generic;
using System.Text;
using TowerDefense3D.Enemies;
using TowerDefense3D.Waves;

namespace TowerDefense3D.GameFlow
{
    public sealed class WaveHudPresenter
    {
        private readonly IWaveSystem waveSystem;
        private readonly IWaveHudView view;
        private string feedback = string.Empty;

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
                state.LivingEnemyCount.ToString("00"),
                CreateStartWaveText(state),
                CreateStartWaveBonusText(state),
                CreatePreviewText(state),
                state.CanStartWave));
        }

        private void HandleStartWaveRequested()
        {
            feedback = waveSystem.TryStartWave(out string error)
                ? string.Empty
                : error;
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

        private string CreatePreviewText(WaveState state)
        {
            if (!string.IsNullOrWhiteSpace(feedback))
            {
                return feedback;
            }

            if (state.Phase == WavePhase.Running)
            {
                return "Network is locked until the wave ends.";
            }

            if (state.Phase == WavePhase.Victory)
            {
                return "All waves cleared.";
            }

            if (state.Phase == WavePhase.Defeat)
            {
                return "The Toad has no HP remaining.";
            }

            IReadOnlyList<EnemySpawnBatchDefinition> batches =
                waveSystem.GetNextWavePreview();
            var summary = new StringBuilder();
            for (int index = 0; index < batches.Count; index++)
            {
                if (index > 0)
                {
                    summary.Append('\n');
                }

                EnemySpawnBatchDefinition batch = batches[index];
                summary.Append("• ")
                    .Append(batch.Enemy.DisplayName)
                    .Append("  ×")
                    .Append(batch.Count)
                    .Append(GetWarning(batch.Enemy));
            }

            return summary.ToString();
        }

        private static string GetWarning(EnemyDefinition enemy)
        {
            if (enemy is SpeedSupportEnemyDefinition)
            {
                return " [SPEED AURA]";
            }

            if (enemy is StealthEnemyDefinition)
            {
                return " [STEALTH]";
            }

            return enemy.Rank == EnemyRank.Regular
                ? string.Empty
                : $" [{enemy.Rank}]";
        }
    }
}
