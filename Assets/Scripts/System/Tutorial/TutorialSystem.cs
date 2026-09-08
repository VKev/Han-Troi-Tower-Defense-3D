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
                progress.MarkCompleted(activeTutorial.Id);
                Stop();
                return;
            }

            QueueCurrentStep();
        }

        public void SetPaused(bool paused)
        {
            isPaused = paused;
            overlay?.SetPaused(paused);
        }

        public void Stop()
        {
            activeTutorial = null;
            steps = null;
            stepIndex = -1;
            delayRemaining = 0f;
            autoCompleteRemaining = 0f;
            showDelayRemaining = 0f;
            stepIsShown = false;
            overlay?.Hide();
            inputGate?.Clear();
            context?.SetGameplayUiMode(TutorialGameplayUiMode.Full);
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
            TutorialStep step = steps[stepIndex];
            context.SetGameplayUiMode(step.GameplayUiMode);
            showDelayRemaining = step.ShowDelaySeconds;
            stepIsShown = false;
            if (showDelayRemaining <= 0f)
            {
                PresentCurrentStep();
            }
        }
    }
}
