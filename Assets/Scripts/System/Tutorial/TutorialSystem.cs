using System;
using System.Collections.Generic;

namespace TowerDefense3D.Tutorials
{
    public sealed class TutorialSystem
    {
        private readonly List<ITutorial> tutorials = new List<ITutorial>();
        private readonly TutorialProgress progress;
        private ITutorialOverlay overlay;
        private ITutorialInputGate inputGate;
        private TutorialContext context;
        private IReadOnlyList<TutorialStep> steps;
        private ITutorial activeTutorial;
        private int stepIndex;
        private float delayRemaining;
        private float autoCompleteRemaining;
        private float showDelayRemaining;
        private bool stepIsShown;
        private bool stepModeApplied;
        private bool isPaused;

        public TutorialSystem(TutorialProgress progress)
        {
            this.progress = progress ?? throw new ArgumentNullException(nameof(progress));
        }

        public bool IsRunning => activeTutorial != null;
        public string ActiveTutorialId => activeTutorial?.Id ?? string.Empty;
        public int ActiveStepIndex => IsRunning ? stepIndex : -1;
        public TutorialProgress Progress => progress;

        public void Register(ITutorial tutorial)
        {
            if (tutorial == null) throw new ArgumentNullException(nameof(tutorial));
            tutorials.Add(tutorial);
            tutorials.Sort((left, right) => right.Priority.CompareTo(left.Priority));
        }

        public void Bind(ITutorialOverlay tutorialOverlay, ITutorialInputGate tutorialInputGate)
        {
            overlay = tutorialOverlay ?? throw new ArgumentNullException(nameof(tutorialOverlay));
            inputGate = tutorialInputGate ?? throw new ArgumentNullException(nameof(tutorialInputGate));
        }

        public void BindLevel(TutorialContext levelContext)
        {
            Stop();
            context = levelContext;
        }

        public void UnbindLevel()
        {
            if (context?.LevelNumber == 1 && !progress.HasCompletedLevelOneTutorial)
            {
                progress.ResetLevelOneSession();
            }

            Stop();
            context = null;
        }

        public void Tick(float deltaTime)
        {
            if (isPaused || context == null || overlay == null || inputGate == null)
            {
                return;
            }

            if (!IsRunning)
            {
                TryStartNext();
                return;
            }

            if (!stepIsShown)
            {
                if (!stepModeApplied && !ActivateQueuedStep())
                {
                    return;
                }

                if (stepIsShown)
                {
                    return;
                }

                showDelayRemaining = Math.Max(0f, showDelayRemaining - deltaTime);
                if (showDelayRemaining <= 0f)
                {
                    PresentCurrentStep();
                }

                return;
            }

            if (delayRemaining > 0f)
            {
                delayRemaining = Math.Max(0f, delayRemaining - deltaTime);
                return;
            }

            TutorialStep step = steps[stepIndex];
            if (step.RequireInstructionComplete
                && !overlay.IsInstructionComplete
                && context.WasPointerPressed)
            {
                overlay.CompleteInstruction();
                return;
            }

            if (step.AutoCompleteSeconds >= 0f)
            {
                autoCompleteRemaining = Math.Max(0f, autoCompleteRemaining - deltaTime);
            }

            if ((step.AutoCompleteSeconds < 0f || autoCompleteRemaining > 0f)
                && !step.Completed(context))
            {
                return;
            }

            if (step.RequireInstructionComplete && !overlay.IsInstructionComplete)
            {
                return;
            }

            stepIndex++;
            if (stepIndex >= steps.Count)
            {
                if (activeTutorial.Id == "fire_tower_v1")
                {
                    progress.CompleteLevelOneTutorial();
                }
                else
                {
                    progress.MarkCompleted(activeTutorial.Id);
                }

                Stop(step.CompletionGameplayUiMode);
                return;
            }

            QueueCurrentStep();
        }

        public void SetPaused(bool paused)
        {
            isPaused = paused;
            overlay?.SetPaused(paused);
        }

        public void Stop(TutorialGameplayUiMode completionGameplayUiMode = TutorialGameplayUiMode.Full)
        {
            activeTutorial = null;
            steps = null;
            stepIndex = -1;
            delayRemaining = 0f;
            autoCompleteRemaining = 0f;
            showDelayRemaining = 0f;
            stepIsShown = false;
            stepModeApplied = false;
            overlay?.Hide();
            inputGate?.Clear();
            context?.SetGameplayUiMode(completionGameplayUiMode);
        }

        private void TryStartNext()
        {
            for (int index = 0; index < tutorials.Count; index++)
            {
                ITutorial candidate = tutorials[index];
                if (progress.IsCompleted(candidate.Id) || !candidate.CanStart(context))
                {
                    continue;
                }

                IReadOnlyList<TutorialStep> candidateSteps = candidate.CreateSteps(context);
                if (candidateSteps == null || candidateSteps.Count == 0)
                {
                    progress.MarkCompleted(candidate.Id);
                    continue;
                }

                activeTutorial = candidate;
                steps = candidateSteps;
                stepIndex = 0;
                QueueCurrentStep();
                return;
            }
        }

        private void PresentCurrentStep()
        {
            TutorialStep step = steps[stepIndex];
            delayRemaining = step.DelaySeconds;
            autoCompleteRemaining = step.AutoCompleteSeconds;
            inputGate.Set(step);
            overlay.Show(step, context);
            stepIsShown = true;
        }

        private void QueueCurrentStep()
        {
            stepIsShown = false;
            stepModeApplied = false;
            showDelayRemaining = 0f;
            if (!ActivateQueuedStep())
            {
                // A deferred step (for example, the Wave 2 prompt while Wave 1 is running) must
                // release the previous spotlight immediately. Otherwise its hand and black mask
                // remain visible while the system waits for the activation condition.
                overlay?.Hide();
                inputGate?.Clear();
            }
            else if (showDelayRemaining > 0f)
            {
                overlay?.Hide();
                inputGate?.Clear();
            }
        }

        private bool ActivateQueuedStep()
        {
            TutorialStep step = steps[stepIndex];
            if (!step.CanShow(context))
            {
                return false;
            }

            context.SetGameplayUiMode(step.GameplayUiMode);
            showDelayRemaining = step.ShowDelaySeconds;
            stepModeApplied = true;
            if (showDelayRemaining <= 0f)
            {
                PresentCurrentStep();
            }

            return true;
        }
    }
}
