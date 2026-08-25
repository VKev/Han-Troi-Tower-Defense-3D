using System;
using System.Collections.Generic;

namespace TowerDefense3D.Core
{
    public sealed class StateMachine<TState> where TState : struct, Enum
    {
        private readonly Func<TState, TState, bool> canTransition;

        public StateMachine(
            TState initialState,
            Func<TState, TState, bool> canTransition)
        {
            CurrentState = initialState;
            this.canTransition = canTransition ?? throw new ArgumentNullException(nameof(canTransition));
        }

        public TState CurrentState { get; private set; }

        public bool CanTransitionTo(TState nextState)
        {
            return EqualityComparer<TState>.Default.Equals(CurrentState, nextState)
                || canTransition(CurrentState, nextState);
        }

        public bool TransitionTo(TState nextState)
        {
            if (EqualityComparer<TState>.Default.Equals(CurrentState, nextState))
            {
                return false;
            }

            if (!canTransition(CurrentState, nextState))
            {
                throw new InvalidOperationException(
                    $"Cannot transition {typeof(TState).Name} from {CurrentState} to {nextState}.");
            }

            CurrentState = nextState;
            return true;
        }
    }
}
