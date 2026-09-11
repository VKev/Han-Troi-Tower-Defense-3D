using System.Collections.Generic;

namespace TowerDefense3D.Tutorials
{
    public sealed class FirstLinkTutorial : ITutorial
    {
        public string Id => "first_link_v2";
        public int Priority => 100;

        public bool CanStart(TutorialContext context)
        {
            return context.LevelNumber == 1
                && context.HasTarget("generator")
                && context.HasTarget("soul_nexus");
        }

        public IReadOnlyList<TutorialStep> CreateSteps(TutorialContext context)
        {
            return new[]
            {
                new TutorialStep(
                    "highlight_next_enemy",
                    "Chuột sắp tấn công",
                    "next_enemy",
                    string.Empty,
                    current => current.IsTrue("tutorial_acknowledged"),
                    0.1f,
                    showDelaySeconds: 1.2f,
                    gameplayUiMode: TutorialGameplayUiMode.PreviewOnly,
                    requireInstructionComplete: true),
                new TutorialStep(
                    "protect_frog",
                    "Hãy bảo vệ cóc bằng cách nối đường đạn bắn kẻ địch",
                    "frog",
                    string.Empty,
                    current => current.IsTrue("tutorial_acknowledged"),
                    0.1f,
                    gameplayUiMode: TutorialGameplayUiMode.FrogOnly,
                    requireInstructionComplete: true),
                new TutorialStep(
                    "link_generator_to_nexus",
                    string.Empty,
                    "generator,soul_nexus",
                    "link_towers",
                    current => current.IsTrue("generator_linked"),
                    0.25f,
                    gameplayUiMode: TutorialGameplayUiMode.PreviewOnly),
                new TutorialStep(
                    "start_first_wave",
                    string.Empty,
                    "start_wave",
                    "start_wave",
                    current => current.IsTrue("wave_running"),
                    0.25f,
                    gameplayUiMode: TutorialGameplayUiMode.StartWaveOnly),
                new TutorialStep(
                    "wave_two_warning",
                    "Có vẻ như phòng thủ chưa đủ,\nhãy đặt thêm Trụ Sinh Đạn",
                    "generator_card,generator_placement",
                    "place_generator",
                    current => current.IsTrue("generator_placed"),
                    0.1f,
                    gameplayUiMode: TutorialGameplayUiMode.GeneratorOnly,
                    requireInstructionComplete: true,
                    canShow: current => current.IsTrue("wave_two_ready"),
                    instructionTargetId: "generator_card"),
                new TutorialStep(
                    "link_new_generator_to_nexus",
                    "Hãy nối Trụ sinh đạn vào Trụ thu đạn",
                    string.Empty,
                    "link_towers",
                    current => current.IsTrue("new_generator_linked"),
                    0.25f,
                    gameplayUiMode: TutorialGameplayUiMode.GeneratorLinkOnly,
                    canShow: current => current.IsTrue("generator_placed"),
                    instructionTargetId: "tower_hud",
                    completionGameplayUiMode: TutorialGameplayUiMode.GeneratorPlacedReady)
            };
        }
    }
}
