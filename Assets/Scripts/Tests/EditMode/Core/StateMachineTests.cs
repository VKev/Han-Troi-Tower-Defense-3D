using System;
using NUnit.Framework;

namespace TowerDefense3D.Core.Tests.EditMode
{
    public sealed class StateMachineTests
    {
        [Test]
        public void TransitionTo_AllowedState_ChangesCurrentState()
        {
            var stateMachine = new StateMachine<TestState>(TestState.Idle, CanTransition);

            bool changed = stateMachine.TransitionTo(TestState.Running);

            Assert.That(changed, Is.True);
            Assert.That(stateMachine.CurrentState, Is.EqualTo(TestState.Running));
        }

        [Test]
        public void TransitionTo_CurrentState_IsNoOp()
        {
            var stateMachine = new StateMachine<TestState>(TestState.Idle, CanTransition);

            bool changed = stateMachine.TransitionTo(TestState.Idle);

            Assert.That(changed, Is.False);
            Assert.That(stateMachine.CurrentState, Is.EqualTo(TestState.Idle));
        }

        [Test]
        public void TransitionTo_DisallowedState_ThrowsWithoutChangingState()
        {
            var stateMachine = new StateMachine<TestState>(TestState.Idle, CanTransition);

            Assert.Throws<InvalidOperationException>(
                () => stateMachine.TransitionTo(TestState.Complete));
            Assert.That(stateMachine.CurrentState, Is.EqualTo(TestState.Idle));
        }

        private static bool CanTransition(TestState currentState, TestState nextState)
        {
            return currentState == TestState.Idle && nextState == TestState.Running
                || currentState == TestState.Running && nextState == TestState.Complete;
        }

        private enum TestState
        {
            Idle,
            Running,
            Complete
        }
    }
}
