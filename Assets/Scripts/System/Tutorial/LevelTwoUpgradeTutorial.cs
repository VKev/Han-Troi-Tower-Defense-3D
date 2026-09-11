using System.Collections.Generic;

namespace TowerDefense3D.Tutorials
{
    public sealed class LevelTwoUpgradeTutorial : ITutorial
    {
        public string Id => "level_two_upgrade_v1";
        public int Priority => 80;

        public bool CanStart(TutorialContext context)
        {
            return context.LevelNumber == 2
                && context.HasTarget("level_two_fire")
                && context.HasTarget("upgrade_button")
                && context.IsTrue("wave_one_ready");
        }

        public IReadOnlyList<TutorialStep> CreateSteps(TutorialContext context)
        {
            return new[]
            {
                new TutorialStep(
                    "select_level_two_fire",
                    "Chạm vào Trụ Lửa để nâng cấp.",
                    "level_two_fire",
                    "select_tower",
                    current => current.IsTrue("level_two_fire_selected"),
                    0.1f,
                    showDelaySeconds: 1.2f,
                    gameplayUiMode: TutorialGameplayUiMode.LevelTwoUpgrade),
                new TutorialStep(
                    "upgrade_level_two_fire",
                    "Bấm Nâng cấp trụ.",
                    "upgrade_button",
                    "upgrade_tower",
                    current => current.IsTrue("level_two_fire_upgraded"),
                    0.1f,
                    showDelaySeconds: 0.1f,
                    gameplayUiMode: TutorialGameplayUiMode.LevelTwoUpgrade)
            };
        }
    }
}
