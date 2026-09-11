using System.Collections.Generic;

namespace TowerDefense3D.Tutorials
{
    public sealed class WaterTowerHintTutorial : ITutorial
    {
        public string Id => "water_tower_hint_v1";
        public int Priority => 60;

        public bool CanStart(TutorialContext context)
        {
            return context.LevelNumber == 1
                && context.IsTrue("wave_five_ready");
        }

        public IReadOnlyList<TutorialStep> CreateSteps(TutorialContext context)
        {
            return new[]
            {
                new TutorialStep(
                    "water_tower_hint",
                    "Hãy dùng Trụ Nước làm ướt kẻ thù tàng hình để làm lộ diện chúng",
                    string.Empty,
                    string.Empty,
                    // The hint is advice for the wave being prepared, so it stays on screen for the
                    // whole preparation and leaves when the player sends the wave in.
                    current => current.IsTrue("wave_running"),
                    requireInstructionComplete: true,
                    instructionTargetId: "tower_hud")
            };
        }
    }
}
