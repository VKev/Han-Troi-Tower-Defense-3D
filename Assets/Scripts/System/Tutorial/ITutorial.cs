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
        bool IsInstructionComplete { get; }
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
        StartWaveOnly
    }

    public interface ITutorialInputGate
    {
        bool IsBlocked { get; }
        bool Allows(string actionId);
        void Set(TutorialStep step);
        void Clear();
    }
}
