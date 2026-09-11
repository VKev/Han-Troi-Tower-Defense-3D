using System.Collections.Generic;

namespace TowerDefense3D.Tutorials
{
    public sealed class LevelTwoThermalShockTutorial : ITutorial
    {
        public string Id => "level_two_thermal_shock_v1";
        public int Priority => 70;

        public bool CanStart(TutorialContext context)
        {
            return context.LevelNumber == 2
                && context.HasTarget("next_enemy")
                && context.HasTarget("water_card")
                && context.HasTarget("fire_card")
                && context.HasTarget("sink_card")
                && context.HasTarget("generator_card")
                && context.IsTrue("wave_three_ready");
        }

        public IReadOnlyList<TutorialStep> CreateSteps(TutorialContext context)
        {
            return new[]
            {
                new TutorialStep(
                    "inspect_magic_resistant",
                    "Kẻ địch tiếp theo có giáp phép, bấm để xem chi tiết.",
                    "next_enemy",
                    "inspect_enemy",
                    current => current.IsTrue("next_enemy_description_open"),
                    0.1f,
                    gameplayUiMode: TutorialGameplayUiMode.LevelTwoEnemyDetailOnly),
                new TutorialStep(
                    "read_magic_resistant",
                    string.Empty,
                    "next_enemy_description",
                    string.Empty,
                    _ => true,
                    2f,
                    gameplayUiMode: TutorialGameplayUiMode.LevelTwoEnemyDetailOnly),
                new TutorialStep(
                    "place_level_two_water",
                    "Có vẻ kẻ địch đã có giáp, phải phá bằng phản ứng Sốc nhiệt",
                    "water_card,level_two_water_placement",
                    "place_water",
                    current => current.IsTrue("level_two_water_placed"),
                    0.1f,
                    gameplayUiMode: TutorialGameplayUiMode.LevelTwoWaterPlacement,
                    requireInstructionComplete: true,
                    instructionTargetId: "tower_hud"),
                new TutorialStep(
                    "place_level_two_fire",
                    string.Empty,
                    "fire_card,level_two_fire_placement",
                    "place_fire",
                    current => current.IsTrue("level_two_fire_placed"),
                    0.1f,
                    gameplayUiMode: TutorialGameplayUiMode.LevelTwoFirePlacement),
                new TutorialStep(
                    "place_level_two_sink",
                    string.Empty,
                    "sink_card,level_two_sink_placement",
                    "place_sink",
                    current => current.IsTrue("level_two_sink_placed"),
                    0.1f,
                    gameplayUiMode: TutorialGameplayUiMode.LevelTwoSinkPlacement),
                new TutorialStep(
                    "place_level_two_generator",
                    string.Empty,
                    "generator_card,level_two_generator_placement",
                    "place_generator",
                    current => current.IsTrue("level_two_generator_placed"),
                    0.1f,
                    gameplayUiMode: TutorialGameplayUiMode.LevelTwoGeneratorPlacement),
                new TutorialStep(
                    "link_level_two_generator_to_fire",
                    string.Empty,
                    "tutorial_level_two_generator,level_two_fire",
                    "link_towers",
                    current => current.IsTrue("level_two_generator_linked_to_fire"),
                    0.1f,
                    gameplayUiMode: TutorialGameplayUiMode.LevelTwoLinking),
                new TutorialStep(
                    "link_level_two_fire_to_water",
                    string.Empty,
                    "level_two_fire,tutorial_level_two_water",
                    "link_towers",
                    current => current.IsTrue("level_two_fire_linked_to_water"),
                    0.1f,
                    gameplayUiMode: TutorialGameplayUiMode.LevelTwoLinking),
                new TutorialStep(
                    "link_level_two_water_to_tutorial_fire",
                    string.Empty,
                    "tutorial_level_two_water,tutorial_level_two_fire",
                    "link_towers",
                    current => current.IsTrue("level_two_water_linked_to_fire"),
                    0.1f,
                    gameplayUiMode: TutorialGameplayUiMode.LevelTwoLinking),
                new TutorialStep(
                    "link_level_two_fire_to_sink",
                    string.Empty,
                    "tutorial_level_two_fire,tutorial_level_two_sink",
                    "link_towers",
                    current => current.IsTrue("level_two_fire_linked_to_sink"),
                    0.1f,
                    gameplayUiMode: TutorialGameplayUiMode.LevelTwoLinking)
            };
        }
    }

    public sealed class LevelTwoCameraZoomTutorial : ITutorial
    {
        public string Id => "level_two_camera_zoom_v1";
        public int Priority => 60;

        public bool CanStart(TutorialContext context)
        {
            return context.LevelNumber == 2 && context.IsTrue("wave_four_ready");
        }

        public IReadOnlyList<TutorialStep> CreateSteps(TutorialContext context)
        {
            return new[]
            {
                new TutorialStep(
                    "level_two_camera_zoom",
                    string.Empty,
                    string.Empty,
                    "zoom_camera",
                    current => current.IsTrue("level_two_zoomed"),
                    gameplayUiMode: TutorialGameplayUiMode.CameraZoom)
            };
        }
    }
}
