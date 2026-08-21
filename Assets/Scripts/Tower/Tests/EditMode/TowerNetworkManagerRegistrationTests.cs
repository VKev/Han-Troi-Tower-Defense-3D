using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using static TowerDefense3D.Towers.TowerRuntimeSpec;

namespace TowerDefense3D.Towers.Tests.EditMode
{
    public sealed class TowerNetworkManagerRegistrationTests
    {
        private readonly List<UnityEngine.Object> owned =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = 0; index < owned.Count; index++)
            {
                UnityEngine.Object.DestroyImmediate(
                    owned[index]);
            }

            owned.Clear();
        }

        [Test]
        public void Manager_IsPlainCSharpAndNotAnotherEntryPoint()
        {
            Assert.That(
                typeof(MonoBehaviour).IsAssignableFrom(
                    typeof(TowerNetworkManager)),
                Is.False);

            Type[] interfaces =
                typeof(TowerNetworkManager).GetInterfaces();

            for (int index = 0; index < interfaces.Length; index++)
            {
                Assert.That(
                    interfaces[index].FullName,
                    Is.Not.EqualTo(
                        "VContainer.Unity.IStartable"));
                Assert.That(
                    interfaces[index].FullName,
                    Is.Not.EqualTo(
                        "VContainer.Unity.ITickable"));
            }
        }

        [Test]
        public void Constructor_CopiesGlobalSimulationRules()
        {
            Fixture fixture = CreateFixture();

            Assert.That(
                fixture.Manager.TickSeconds,
                Is.EqualTo(0.05f));
            Assert.That(
                fixture.Manager.ProjectileSpeedMetersPerSecond,
                Is.EqualTo(10f));
            Assert.That(
                fixture.Manager.MaximumLinkRangeMeters,
                Is.EqualTo(12f));
        }

        [Test]
        public void BeginSession_StoresLevelAndStartsEmpty()
        {
            Fixture fixture = CreateFixture();

            fixture.Manager.BeginLevelSession(2);

            Assert.That(
                fixture.Manager.HasLevelSession,
                Is.True);
            Assert.That(
                fixture.Manager.ActiveLevelNumber,
                Is.EqualTo(2));
            Assert.That(
                fixture.Manager.NodeCount,
                Is.Zero);
        }

        [Test]
        public void BeginSession_WhenAlreadyActive_Throws()
        {
            Fixture fixture = CreateFixture();
            fixture.Manager.BeginLevelSession(1);

            Assert.Throws<InvalidOperationException>(
                () => fixture.Manager.BeginLevelSession(2));

            Assert.That(
                fixture.Manager.ActiveLevelNumber,
                Is.EqualTo(1));
        }

        [Test]
        public void RegisterTower_RequiresActiveSession()
        {
            Fixture fixture = CreateFixture();

            Assert.Throws<InvalidOperationException>(
                () => fixture.Manager.RegisterTower(
                    fixture.Generator,
                    Position(0f)));
        }

        [Test]
        public void RegisterTower_AssignsOrderedDeterministicIds()
        {
            Fixture fixture = CreateFixture();
            fixture.Manager.BeginLevelSession(1);

            TowerNodeId generator =
                fixture.Manager.RegisterTower(
                    fixture.Generator,
                    Position(0f));

            TowerNodeId fire =
                fixture.Manager.RegisterTower(
                    fixture.Fire,
                    Position(1f));

            TowerNodeId nexus =
                fixture.Manager.RegisterTower(
                    fixture.Nexus,
                    Position(2f));

            Assert.That(generator.Value, Is.EqualTo(1));
            Assert.That(fire.Value, Is.EqualTo(2));
            Assert.That(nexus.Value, Is.EqualTo(3));

            CollectionAssert.AreEqual(
                new[] { generator, fire, nexus },
                fixture.Manager.CreateNodeIdSnapshot());
        }

        [Test]
        public void RegisteredNode_ContainsCopiedSpecAndPosition()
        {
            Fixture fixture = CreateFixture();
            fixture.Manager.BeginLevelSession(1);

            TowerNodeId fire =
                fixture.Manager.RegisterTower(
                    fixture.Fire,
                    new TowerWorldPosition(
                        3f,
                        4f,
                        5f));

            Assert.That(
                fixture.Manager.TryGetNodeSpec(
                    fire,
                    out TowerRuntimeSpec spec),
                Is.True);

            Assert.That(
                spec.Family,
                Is.EqualTo(TowerFamily.Fire));
            Assert.That(spec.CycleTicks, Is.EqualTo(17));

            Assert.That(
                fixture.Manager.TryGetNodePosition(
                    fire,
                    out TowerWorldPosition position),
                Is.True);

            Assert.That(position.X, Is.EqualTo(3f));
            Assert.That(position.Y, Is.EqualTo(4f));
            Assert.That(position.Z, Is.EqualTo(5f));
        }

        [Test]
        public void Unregister_RemovesNodeButDoesNotReuseItsId()
        {
            Fixture fixture = CreateFixture();
            fixture.Manager.BeginLevelSession(1);

            TowerNodeId generator =
                fixture.Manager.RegisterTower(
                    fixture.Generator,
                    Position(0f));

            TowerNodeId fire =
                fixture.Manager.RegisterTower(
                    fixture.Fire,
                    Position(1f));

            Assert.That(
                fixture.Manager.UnregisterTower(fire),
                Is.True);

            TowerNodeId nexus =
                fixture.Manager.RegisterTower(
                    fixture.Nexus,
                    Position(2f));

            Assert.That(generator.Value, Is.EqualTo(1));
            Assert.That(fire.Value, Is.EqualTo(2));
            Assert.That(nexus.Value, Is.EqualTo(3));

            CollectionAssert.AreEqual(
                new[] { generator, nexus },
                fixture.Manager.CreateNodeIdSnapshot());
        }

        [Test]
        public void EndSession_ClearsNodesAndNextSessionRestartsIds()
        {
            Fixture fixture = CreateFixture();
            fixture.Manager.BeginLevelSession(1);

            fixture.Manager.RegisterTower(
                fixture.Generator,
                Position(0f));

            fixture.Manager.RegisterTower(
                fixture.Nexus,
                Position(1f));

            fixture.Manager.EndLevelSession();

            Assert.That(
                fixture.Manager.HasLevelSession,
                Is.False);
            Assert.That(fixture.Manager.NodeCount, Is.Zero);
            Assert.That(
                fixture.Manager.CreateNodeIdSnapshot(),
                Is.Empty);

            fixture.Manager.BeginLevelSession(2);

            TowerNodeId firstInLevelTwo =
                fixture.Manager.RegisterTower(
                    fixture.Generator,
                    Position(0f));

            Assert.That(
                firstInLevelTwo.Value,
                Is.EqualTo(1));
        }

        [Test]
        public void RegisterTower_RejectsNonFinitePosition()
        {
            Fixture fixture = CreateFixture();
            fixture.Manager.BeginLevelSession(1);

            Assert.Throws<ArgumentException>(
                () => fixture.Manager.RegisterTower(
                    fixture.Generator,
                    new TowerWorldPosition(
                        float.NaN,
                        0f,
                        0f)));

            Assert.That(fixture.Manager.NodeCount, Is.Zero);
        }

        [Test]
        public void StateChanged_FiresOnlyAfterSuccessfulMutation()
        {
            Fixture fixture = CreateFixture();
            int eventCount = 0;

            fixture.Manager.StateChanged +=
                () => eventCount++;

            fixture.Manager.BeginLevelSession(1);

            TowerNodeId generator =
                fixture.Manager.RegisterTower(
                    fixture.Generator,
                    Position(0f));

            Assert.That(
                fixture.Manager.UnregisterTower(generator),
                Is.True);

            Assert.That(
                fixture.Manager.UnregisterTower(generator),
                Is.False);

            fixture.Manager.EndLevelSession();
            fixture.Manager.EndLevelSession();

            Assert.That(eventCount, Is.EqualTo(4));
        }

        [Test]
        public void RegisteredTowers_CreateBuffersFromRuntimeSpecs()
        {
            Fixture fixture = CreateFixture();

            fixture.Manager.BeginLevelSession(1);

            TowerNodeId generator =
                fixture.Manager.RegisterTower(
                    fixture.Generator,
                    Position(0f));

            TowerNodeId fire =
                fixture.Manager.RegisterTower(
                    fixture.Fire,
                    Position(1f));

            TowerNodeId nexus =
                fixture.Manager.RegisterTower(
                    fixture.Nexus,
                    Position(2f));

            Assert.That(
                fixture.Manager.TryCreateInputPortSnapshot(
                    generator,
                    0,
                    out _),
                Is.False);

            Assert.That(
                fixture.Manager.TryCreateInputPortSnapshot(
                    fire,
                    0,
                    out TowerInputPortSnapshot firePort),
                Is.True);

            Assert.That(
                firePort.Capacity,
                Is.EqualTo(3));

            Assert.That(
                firePort.QueuedProjectileCount,
                Is.Zero);

            Assert.That(
                firePort.ReservedProjectileCount,
                Is.Zero);

            Assert.That(
                fixture.Manager.TryCreateInputPortSnapshot(
                    nexus,
                    0,
                    out TowerInputPortSnapshot nexusPortZero),
                Is.True);

            Assert.That(
                fixture.Manager.TryCreateInputPortSnapshot(
                    nexus,
                    1,
                    out TowerInputPortSnapshot nexusPortOne),
                Is.True);

            Assert.That(
                nexusPortZero.Capacity,
                Is.EqualTo(4));

            Assert.That(
                nexusPortOne.Capacity,
                Is.EqualTo(4));

            Assert.That(
                nexusPortZero.AvailableSlotCount,
                Is.EqualTo(4));

            Assert.That(
                nexusPortOne.AvailableSlotCount,
                Is.EqualTo(4));
        }

        private Fixture CreateFixture()
        {
            TowerCombatRules rules =
                Create<TowerCombatRules>();

            GeneratorTowerDefinition generator =
                Create<GeneratorTowerDefinition>();

            FireTowerDefinition fire =
                Create<FireTowerDefinition>();

            WaterTowerDefinition water =
                Create<WaterTowerDefinition>();

            WindTowerDefinition wind =
                Create<WindTowerDefinition>();

            EarthTowerDefinition earth =
                Create<EarthTowerDefinition>();

            SoulNexusDefinition nexus =
                Create<SoulNexusDefinition>();

            TowerCatalog catalog =
                Create<TowerCatalog>();

            SetPrivateField(
                catalog,
                "combatRules",
                rules);

            SetPrivateField(
                catalog,
                "definitions",
                new List<TowerCombatDefinition>
                {
                    generator,
                    fire,
                    water,
                    wind,
                    earth,
                    nexus
                });

            return new Fixture(
                new TowerNetworkManager(catalog),
                generator,
                fire,
                nexus);
        }

        private T Create<T>() where T : ScriptableObject
        {
            T instance =
                ScriptableObject.CreateInstance<T>();

            owned.Add(instance);
            return instance;
        }

        private static void SetPrivateField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field =
                target.GetType().GetField(
                    fieldName,
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);

            Assert.That(
                field,
                Is.Not.Null,
                $"Missing field '{fieldName}'.");

            field.SetValue(target, value);
        }

        private static TowerWorldPosition Position(float x)
        {
            return new TowerWorldPosition(
                x,
                0f,
                0f);
        }

        private readonly struct Fixture
        {
            public Fixture(
                TowerNetworkManager manager,
                GeneratorTowerDefinition generator,
                FireTowerDefinition fire,
                SoulNexusDefinition nexus)
            {
                Manager = manager;
                Generator = generator;
                Fire = fire;
                Nexus = nexus;
            }

            public TowerNetworkManager Manager { get; }
            public GeneratorTowerDefinition Generator { get; }
            public FireTowerDefinition Fire { get; }
            public SoulNexusDefinition Nexus { get; }
        }
    }
}