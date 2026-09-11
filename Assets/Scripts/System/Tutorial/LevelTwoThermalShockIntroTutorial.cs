using System.Collections.Generic;

namespace TowerDefense3D.Tutorials
{
    /// <summary>
    /// Stages the first thermal shock of Level 2's wave 3: the board slows, the camera leans in on
    /// the enemy it is about to land on, and the line explaining the reaction reads while it
    /// actually happens.
    /// </summary>
    /// <remarks>
    /// Deliberately not a frozen beat like the burn introduction in Level 1. That one points at a
    /// state the enemy is already in, so stopping the board loses nothing. This one is about an
    /// event still to come, and a frozen board could only ever describe it - the player would read
    /// about thermal shock and then have to take it on trust. Slowed instead, the sentence and the
    /// reaction land together.
    ///
    /// The beat is opened by the focus system spotting the reaction in the planned timeline a
    /// fixed number of ticks before it is due, which is possible only because the whole wave is
    /// planned before it is played.
    /// </remarks>
    public sealed class LevelTwoThermalShockIntroTutorial : ITutorial
    {
        public string Id => "level_two_thermal_shock_intro_v1";

        /// <summary>
        /// Above the wave-3 build tutorial, which has finished by the time a wave is running, and
        /// below nothing else that can fire mid-wave.
        /// </summary>
        public int Priority => 95;

        public bool CanStart(TutorialContext context)
        {
            return context.LevelNumber == 2
                && context.IsTrue("thermal_shock_incoming");
        }

        public IReadOnlyList<TutorialStep> CreateSteps(TutorialContext context)
        {
            return new[]
            {
                new TutorialStep(
                    "thermal_shock_intro",
                    "Nguyên tố Lửa gặp Nước sẽ gây ra phản ứng Sốc Nhiệt",
                    "thermal_shock_enemy",
                    string.Empty,
                    // Held until the reaction has actually landed, so the beat cannot close on a
                    // promise the player has not been shown yet.
                    current => current.IsTrue("thermal_shock_resolved"),
                    // Covers the dolly-in, so the spotlight is measured against the camera where
                    // it ends up rather than where it started.
                    showDelaySeconds: 0.4f,
                    gameplayUiMode: TutorialGameplayUiMode.ThermalShockSlowMotion,
                    requireInstructionComplete: true,
                    instructionTargetId: "tower_hud"),
                new TutorialStep(
                    "thermal_shock_hold",
                    string.Empty,
                    "thermal_shock_enemy",
                    string.Empty,
                    _ => true,
                    delaySeconds: 0.5f,
                    gameplayUiMode: TutorialGameplayUiMode.ThermalShockSlowMotion,
                    completionGameplayUiMode: TutorialGameplayUiMode.Full,
                    keepInstructionVisible: true)
            };
        }
    }
}
