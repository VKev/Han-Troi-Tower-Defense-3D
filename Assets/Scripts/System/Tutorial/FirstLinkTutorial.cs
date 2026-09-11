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
                // Wave 2 is the wave the boar first walks in on, so it is introduced here rather
                // than at wave 3 where it used to be - by then the player has already fought one.
                // The portrait it points at is the preview's first slot, which is the boar because
                // the wave authors its batch before the rats.
                new TutorialStep(
                    "inspect_next_enemy",
                    "Kẻ địch kế tiếp là heo rừng, bấm để xem chi tiết",
                    "next_enemy",
                    "inspect_enemy",
                    current => current.IsTrue("next_enemy_description_open"),
                    0.1f,
                    gameplayUiMode: TutorialGameplayUiMode.EnemyDetailOnly,
                    canShow: current => current.IsTrue("wave_two_ready")),
                new TutorialStep(
                    "read_enemy_description",
                    string.Empty,
                    "next_enemy_description",
                    string.Empty,
                    _ => true,
                    2f,
                    gameplayUiMode: TutorialGameplayUiMode.EnemyDetailOnly),
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
