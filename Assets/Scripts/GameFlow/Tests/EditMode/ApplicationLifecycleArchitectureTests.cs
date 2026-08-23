using System;
using System.Linq;
using NUnit.Framework;
using TowerDefense3D.GridPlacement;
using TowerDefense3D.Mobile;
using UnityEngine;
using VContainer.Unity;

namespace TowerDefense3D.GameFlow.Tests.EditMode
{
    public sealed class ApplicationLifecycleArchitectureTests
    {
        [Test]
        public void ApplicationEntryPoint_IsTheOnlyProjectVContainerLifecycleType()
        {
            Type[] projectTypes = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => assembly.GetName().Name.StartsWith("TowerDefense3D", StringComparison.Ordinal))
                .SelectMany(assembly => assembly.GetTypes())
                .ToArray();

            Type[] lifecycleTypes = projectTypes
                .Where(type => typeof(IAsyncStartable).IsAssignableFrom(type)
                    || typeof(IStartable).IsAssignableFrom(type)
                    || typeof(ITickable).IsAssignableFrom(type)
                    || typeof(ILateTickable).IsAssignableFrom(type))
                .ToArray();

            CollectionAssert.AreEquivalent(new[] { typeof(ApplicationEntryPoint) }, lifecycleTypes);
        }

        [Test]
        public void Systems_DoNotInheritMonoBehaviour()
        {
            Assert.That(typeof(MonoBehaviour).IsAssignableFrom(typeof(FramePacingSystem)), Is.False);
            Assert.That(typeof(MonoBehaviour).IsAssignableFrom(typeof(SafeAreaSystem)), Is.False);
            Assert.That(typeof(MonoBehaviour).IsAssignableFrom(typeof(BoardSystem)), Is.False);
            Assert.That(typeof(MonoBehaviour).IsAssignableFrom(typeof(BoardCameraSystem)), Is.False);
        }

        [Test]
        public void ActiveLevelSystemSlot_RejectsOverlappingGroupsAndOnlyDetachesItsOwner()
        {
            var slot = new ActiveLevelSystemSlot();
            var first = new LevelSystemGroup(null, null);
            var second = new LevelSystemGroup(null, null);

            slot.Attach(first);

            Assert.That(slot.HasActiveLevel, Is.True);
            Assert.Throws<InvalidOperationException>(() => slot.Attach(second));
            Assert.Throws<InvalidOperationException>(() => slot.Detach(second));

            slot.Detach(first);

            Assert.That(slot.HasActiveLevel, Is.False);
        }

        [Test]
        public void SafeAreaSystem_AppliesOnlyWhenScreenInputsChange()
        {
            var view = new RecordingSafeAreaView(
                new Rect(100f, 50f, 800f, 400f),
                new Vector2Int(1000, 500));
            var system = new SafeAreaSystem(view);

            system.Start();
            system.Tick();

            Assert.That(view.ApplyCount, Is.EqualTo(1));
            Assert.That(view.AnchorMin, Is.EqualTo(new Vector2(0.1f, 0.1f)));
            Assert.That(view.AnchorMax, Is.EqualTo(new Vector2(0.9f, 0.9f)));

            view.SafeArea = new Rect(0f, 0f, 1000f, 500f);
            system.Tick();

            Assert.That(view.ApplyCount, Is.EqualTo(2));
            Assert.That(view.AnchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(view.AnchorMax, Is.EqualTo(Vector2.one));
        }

        private sealed class RecordingSafeAreaView : ISafeAreaView
        {
            public RecordingSafeAreaView(Rect safeArea, Vector2Int screenSize)
            {
                SafeArea = safeArea;
                ScreenSize = screenSize;
            }

            public Rect SafeArea { get; set; }
            public Vector2Int ScreenSize { get; }
            public int ApplyCount { get; private set; }
            public Vector2 AnchorMin { get; private set; }
            public Vector2 AnchorMax { get; private set; }

            public void ApplyAnchors(Vector2 anchorMin, Vector2 anchorMax)
            {
                ApplyCount++;
                AnchorMin = anchorMin;
                AnchorMax = anchorMax;
            }
        }
    }
}
