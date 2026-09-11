using System.Collections.Generic;

namespace TowerDefense3D.Tutorials
{
    public sealed class FireBurnStatusTutorial : ITutorial
    {
        public string Id => "fire_burn_status_v1";
        public int Priority => 75;

        public bool CanStart(TutorialContext context)
        {
            return context.LevelNumber == 1
                && context.IsTrue("first_fire_hit");
        }

        public IReadOnlyList<TutorialStep> CreateSteps(TutorialContext context)
        {
            return new[]
            {
                new TutorialStep(
                    "burn_status_focus",
                    "Đạn lửa đã làm kẻ thù bị cháy rồi",
                    "burning_enemy_icon",
                    string.Empty,
                    _ => true,
                    // The show delay covers the dolly-in, so the spotlight rect is measured
                    // against the camera where it ends up rather than where it started.
                    showDelaySeconds: 0.55f,
                    gameplayUiMode: TutorialGameplayUiMode.BurnStatusFrozen,
                    requireInstructionComplete: true),
                new TutorialStep(
                    "burn_status_hold",
                    string.Empty,
                    "burning_enemy_icon",
                    string.Empty,
                    _ => true,
                    delaySeconds: 1.5f,
                    gameplayUiMode: TutorialGameplayUiMode.BurnStatusFrozen,
                    completionGameplayUiMode: TutorialGameplayUiMode.Full,
                    keepInstructionVisible: true)
            };
        }
    }
}
