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
                // The boar is not introduced here any more. Wave 2 is the wave it first walks in
                // on, so meeting it at wave 3 would have been a wave late - see FirstLinkTutorial,
                // which now carries the introduction.
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
