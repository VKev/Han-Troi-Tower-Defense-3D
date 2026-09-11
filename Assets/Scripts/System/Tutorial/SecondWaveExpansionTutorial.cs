using System.Collections.Generic;

namespace TowerDefense3D.Tutorials
{
    public sealed class SecondWaveExpansionTutorial : ITutorial
    {
        public string Id => "second_wave_expansion_v1";
        public int Priority => 90;

        public bool CanStart(TutorialContext context)
        {
            return context.LevelNumber == 1
                && context.HasTarget("generator_card")
                && context.HasTarget("sink_card")
                && context.IsTrue("wave_three_ready");
        }

        public IReadOnlyList<TutorialStep> CreateSteps(TutorialContext context)
        {
            return new[]
            {
                new TutorialStep(
                    "inspect_next_enemy",
                    "Kẻ địch kế tiếp là heo rừng, bấm để xem chi tiết",
                    "next_enemy",
                    "inspect_enemy",
                    current => current.IsTrue("next_enemy_description_open"),
                    0.1f,
                    gameplayUiMode: TutorialGameplayUiMode.EnemyDetailOnly),
                new TutorialStep(
                    "read_enemy_description",
                    string.Empty,
                    "next_enemy_description",
                    string.Empty,
                    _ => true,
                    2f,
                    gameplayUiMode: TutorialGameplayUiMode.EnemyDetailOnly),
                new TutorialStep(
                    "enemy_strength_warning",
                    "Kẻ địch mạnh hơn rồi, hãy đặt thêm Trụ thu đạn và Trụ sinh đạn",
                    "sink_card,sink_placement",
                    "place_sink",
                    current => current.IsTrue("second_sink_placed"),
                    0.25f,
                    gameplayUiMode: TutorialGameplayUiMode.SinkPlacementOnly,
                    instructionTargetId: "tower_hud"),
                new TutorialStep(
                    "place_second_generator",
                    string.Empty,
                    "generator_card,second_generator_placement",
                    "place_generator",
                    current => current.IsTrue("second_generator_placed"),
                    0.25f,
                    gameplayUiMode: TutorialGameplayUiMode.SecondGeneratorPlacementOnly,
                    completionGameplayUiMode: TutorialGameplayUiMode.GeneratorAndSinkReady,
                    keepInstructionVisible: true)
            };
        }
    }
}
