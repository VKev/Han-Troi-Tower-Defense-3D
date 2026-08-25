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
                CreateWaveText(state),
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

        private static string CreateWaveText(WaveState state)
        {
            switch (state.Phase)
            {
                case WavePhase.Running:
                    return $"WAVE {state.CurrentWaveNumber}/{state.WaveCount}"
                        + $"  ENEMIES {state.LivingEnemyCount}";
                case WavePhase.Victory:
                    return "VICTORY";
                default:
                    return $"START WAVE {state.CurrentWaveNumber}/{state.WaveCount}";
            }
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

            IReadOnlyList<EnemySpawnBatchDefinition> batches =
                waveSystem.GetNextWavePreview();
            var summary = new StringBuilder("Next: ");
            for (int index = 0; index < batches.Count; index++)
            {
                if (index > 0)
                {
                    summary.Append("  |  ");
                }

                EnemySpawnBatchDefinition batch = batches[index];
                summary.Append(batch.Count)
                    .Append("x ")
                    .Append(batch.Enemy.DisplayName)
                    .Append(GetWarning(batch.Enemy));
            }

            return summary.ToString();
        }

        private static string GetWarning(EnemyDefinition enemy)
        {
            if (enemy is ShortcutBuilderEnemyDefinition)
            {
                return " [SHORTCUT]";
            }

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
