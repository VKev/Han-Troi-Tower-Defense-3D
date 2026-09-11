using System.Collections.Generic;

namespace TowerDefense3D.Tutorials
{
    public sealed class FireTowerTutorial : ITutorial
    {
        public string Id => "fire_tower_v1";
        public int Priority => 80;

        public bool CanStart(TutorialContext context)
        {
            return context.LevelNumber == 1
                && context.HasTarget("fire_card")
                && context.IsTrue("wave_four_ready");
        }

        public IReadOnlyList<TutorialStep> CreateSteps(TutorialContext context)
        {
            return new[]
            {
                new TutorialStep(
                    "fire_tower_intro",
                    "Được rồi, có vẻ như kẻ địch đã mạnh hơn, đã tới lúc sử dụng Nguyên Tố Lửa",
                    "fire_card,fire_placement",
                    "place_fire",
                    current => current.IsTrue("fire_placed"),
                    0.25f,
                    gameplayUiMode: TutorialGameplayUiMode.FirePlacementOnly,
                    requireInstructionComplete: true,
                    instructionTargetId: "tower_hud"),
                new TutorialStep(
                    "select_generator_to_unlink",
                    "Chạm vào Trụ sinh đạn để chỉnh sửa liên kết.",
                    "tutorial_second_generator",
                    "select_generator",
                    current => current.IsTrue("second_generator_selected"),
                    0.25f,
                    gameplayUiMode: TutorialGameplayUiMode.GeneratorUnlinkOnly),
                new TutorialStep(
                    "unlink_generator",
                    "Bấm Hủy liên kết.",
                    "tutorial_second_generator,unlink_button",
                    "unlink_tower",
                    current => current.IsTrue("second_generator_unlinked"),
                    0.25f,
                    showDelaySeconds: 0.1f,
                    gameplayUiMode: TutorialGameplayUiMode.GeneratorUnlinkOnly),
                new TutorialStep(
                    "link_generator_to_fire",
                    string.Empty,
                    "tutorial_second_generator,tutorial_fire",
                    "link_towers",
                    current => current.IsTrue("generator_linked_to_fire"),
                    0.25f,
                    gameplayUiMode: TutorialGameplayUiMode.FireLinkFromGeneratorOnly),
                new TutorialStep(
                    "link_fire_to_sink",
                    string.Empty,
                    "tutorial_fire,tutorial_sink",
                    "link_towers",
                    current => current.IsTrue("fire_linked_to_sink"),
                    0.25f,
                    gameplayUiMode: TutorialGameplayUiMode.FireLinkToSinkOnly,
                    completionGameplayUiMode: TutorialGameplayUiMode.GeneratorSinkAndElementsReady)
            };
        }
    }
}
