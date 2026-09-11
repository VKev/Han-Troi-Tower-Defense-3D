using System;

namespace TowerDefense3D.Tutorials
{
    public sealed class TutorialStep
    {
        public TutorialStep(
            string id,
            string instruction,
            string targetId,
            string actionId,
            Func<TutorialContext, bool> completed,
            float delaySeconds = 0f,
            float autoCompleteSeconds = -1f,
            float showDelaySeconds = 0f,
            TutorialGameplayUiMode gameplayUiMode = TutorialGameplayUiMode.Full,
            bool requireInstructionComplete = false,
            Func<TutorialContext, bool> canShow = null,
            string instructionTargetId = null,
            TutorialGameplayUiMode completionGameplayUiMode = TutorialGameplayUiMode.Full,
            bool keepInstructionVisible = false)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Instruction = instruction ?? string.Empty;
            TargetId = targetId ?? string.Empty;
            ActionId = actionId ?? string.Empty;
            Completed = completed ?? throw new ArgumentNullException(nameof(completed));
            DelaySeconds = Math.Max(0f, delaySeconds);
            AutoCompleteSeconds = autoCompleteSeconds;
            ShowDelaySeconds = Math.Max(0f, showDelaySeconds);
            GameplayUiMode = gameplayUiMode;
            RequireInstructionComplete = requireInstructionComplete;
            CanShow = canShow ?? (_ => true);
            InstructionTargetId = instructionTargetId ?? string.Empty;
            CompletionGameplayUiMode = completionGameplayUiMode;
            KeepInstructionVisible = keepInstructionVisible;
        }

        public string Id { get; }
        public string Instruction { get; }
        public string TargetId { get; }
        public string ActionId { get; }
        public float DelaySeconds { get; }
        public float AutoCompleteSeconds { get; }
        public float ShowDelaySeconds { get; }
        public TutorialGameplayUiMode GameplayUiMode { get; }
        public bool RequireInstructionComplete { get; }
        public Func<TutorialContext, bool> CanShow { get; }
        public string InstructionTargetId { get; }
        public TutorialGameplayUiMode CompletionGameplayUiMode { get; }
        public bool KeepInstructionVisible { get; }
        public Func<TutorialContext, bool> Completed { get; }
    }
}
