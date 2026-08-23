using NUnit.Framework;
using TowerDefense3D.GameplayInput;
using UnityEngine;

namespace TowerDefense3D.GridPlacement.Tests.EditMode
{
    public sealed class GameplayInputSystemTests
    {
        [Test]
        public void Tick_CapturesOneSnapshotAndKeepsExplicitModeOwnership()
        {
            var expected = new GameplayInputSnapshot(
                cancelRequested: false,
                wasInterrupted: false,
                hasPointerInput: true,
                wasPressed: true,
                isPressed: true,
                wasReleased: false,
                pointerId: 7,
                screenPosition: new Vector2(12f, 34f),
                isPointerOverUi: false);
            var source = new StubGameplayInputSource(expected);
            var system = new GameplayInputSystem(source);

            system.SetMode(GameplayInputMode.GridPlacement);
            system.Tick();

            Assert.That(source.CaptureCount, Is.EqualTo(1));
            Assert.That(system.Current.PointerId, Is.EqualTo(7));
            Assert.That(system.Current.ScreenPosition, Is.EqualTo(new Vector2(12f, 34f)));
            Assert.That(system.Mode, Is.EqualTo(GameplayInputMode.GridPlacement));

            system.ClearMode(GameplayInputMode.TowerInteraction);
            Assert.That(system.Mode, Is.EqualTo(GameplayInputMode.GridPlacement));

            system.ClearMode(GameplayInputMode.GridPlacement);
            Assert.That(system.Mode, Is.EqualTo(GameplayInputMode.None));
        }

        private sealed class StubGameplayInputSource : IGameplayInputSource
        {
            private readonly GameplayInputSnapshot snapshot;

            public StubGameplayInputSource(GameplayInputSnapshot snapshot)
            {
                this.snapshot = snapshot;
            }

            public int CaptureCount { get; private set; }

            public GameplayInputSnapshot Capture()
            {
                CaptureCount++;
                return snapshot;
            }
        }
    }
}
