using System;
using System.Collections.Generic;

namespace TowerDefense3D.Tutorials
{
    public interface ITutorial
    {
        string Id { get; }
        int Priority { get; }
        bool CanStart(TutorialContext context);
        IReadOnlyList<TutorialStep> CreateSteps(TutorialContext context);
    }

    public interface ITutorialOverlay
    {
        event Action<bool> BlackOverlayVisibilityChanged;

        bool IsInstructionComplete { get; }
        bool IsBlackOverlayVisible { get; }
        void CompleteInstruction();
        void Show(TutorialStep step, TutorialContext context);
        void Hide();
        void SetPaused(bool paused);
    }

    public enum TutorialGameplayUiMode
    {
        Full,
        PreviewOnly,
        FrogOnly,
        StartWaveOnly,
        GeneratorOnly,
        GeneratorLinkOnly,
        GeneratorPlacedReady,
        SinkPlacementOnly,
        SecondGeneratorPlacementOnly,
        GeneratorAndSinkReady,
        EnemyDetailOnly,
        FirePlacementOnly,
        GeneratorUnlinkOnly,
        FireLinkFromGeneratorOnly,
        FireLinkToSinkOnly,
        GeneratorSinkAndElementsReady,
        LevelTwoUpgrade,
        LevelTwoEnemyDetailOnly,
        LevelTwoWaterPlacement,
        LevelTwoFirePlacement,
        LevelTwoSinkPlacement,
        LevelTwoGeneratorPlacement,
        LevelTwoLinking,
        BurnStatusFrozen,

        /// <summary>
        /// The board runs slowly while a reaction the player is meant to watch lands.
        /// </summary>
        ThermalShockSlowMotion
    }

    public interface ITutorialInputGate
    {
        bool IsBlocked { get; }
        bool Allows(string actionId);
        void Set(TutorialStep step);
        void Clear();
    }
}
