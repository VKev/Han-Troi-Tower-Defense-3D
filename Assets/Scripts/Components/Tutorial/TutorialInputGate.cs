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
            IsBlocked = true;
        }

        public void Clear()
        {
            allowedActions.Clear();
            IsBlocked = false;
        }
    }
}
