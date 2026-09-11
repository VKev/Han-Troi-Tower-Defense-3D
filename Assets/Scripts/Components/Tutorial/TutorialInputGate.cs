using System.Collections.Generic;
using TowerDefense3D.Tutorials;
using UnityEngine;

namespace TowerDefense3D.GameFlow
{
    public sealed class TutorialInputGate : MonoBehaviour, ITutorialInputGate
    {
        private readonly HashSet<string> allowedActions = new HashSet<string>();

        public bool IsBlocked { get; private set; }

        public bool Allows(string actionId)
        {
            return !IsBlocked || allowedActions.Contains(actionId);
        }

        public void Set(TutorialStep step)
        {
            allowedActions.Clear();
            if (!string.IsNullOrEmpty(step.ActionId)) allowedActions.Add(step.ActionId);

            // A step that names no action is telling the player something rather than asking for
            // a gesture, so it must not restrict anything. All this gate governs is link
            // dragging, and an informational step can stand for a whole preparation phase - the
            // water hint waits for the wave to start - which silently killed linking throughout.
            IsBlocked = allowedActions.Count > 0;
        }

        public void Clear()
        {
            allowedActions.Clear();
            IsBlocked = false;
        }
    }
}
