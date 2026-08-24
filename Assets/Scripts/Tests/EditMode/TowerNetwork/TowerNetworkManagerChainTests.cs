using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace TowerDefense3D.Towers.Tests.EditMode
{
    public sealed class TowerNetworkManagerChainTests
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
        public void GeneratorToNexus_IsMinimumValidChain()
        {
            Fixture fixture = CreateFixture();
            fixture.Manager.BeginLevelSession(1);

            TowerNodeId generator =
                fixture.Manager.RegisterTower(
                    fixture.Generator,
                    Position(0f));

            TowerNodeId nexus =
                fixture.Manager.RegisterTower(
                    fixture.Nexus,
                    Position(1f));

            Assert.That(
                fixture.Manager.TryRewire(
                    generator,
                    nexus,
                    out string error),
                Is.True,
                error);

            Assert.That(
                fixture.Manager.ValidChainCount,
                Is.EqualTo(1));
            Assert.That(
                fixture.Manager.HasValidChain,
                Is.True);

            CollectionAssert.AreEqual(
                new[] { generator, nexus },
                fixture.Manager
                    .CreateValidNodeIdSnapshot());
        }

        [Test]
        public void StartSimulation_RequiresValidChain()
        {
            Fixture fixture = CreateFixture();

            fixture.Manager.BeginLevelSession(1);

            fixture.Manager.RegisterTower(
                fixture.Generator,
                Position(0f));

            Assert.That(
                fixture.Manager.TryStartSimulation(
                    out string error),
                Is.False);

            Assert.That(
                fixture.Manager.IsRunning,
                Is.False);

            Assert.That(
                fixture.Manager.CurrentTick,
                Is.Zero);

            Assert.That(
                error,
                Is.Not.Empty);
        }

        [Test]
        public void DirectGeneratorToNexus_CanStartSimulation()
        {
            Fixture fixture = CreateFixture();

            fixture.Manager.BeginLevelSession(1);

            TowerNodeId generator =
                fixture.Manager.RegisterTower(
                    fixture.Generator,
                    Position(0f));

            TowerNodeId nexus =
                fixture.Manager.RegisterTower(
                    fixture.Nexus,
                    Position(1f));

            Assert.That(
                fixture.Manager.TryRewire(
                    generator,
                    nexus,
                    out string linkError),
                Is.True,
                linkError);

            Assert.That(
                fixture.Manager.TryStartSimulation(
                    out string startError),
                Is.True,
                startError);

            Assert.That(
                fixture.Manager.IsRunning,
                Is.True);

            Assert.That(
                fixture.Manager.CurrentTick,
                Is.Zero);
        }

        [Test]
        public void StepOneTick_AdvancesOnlyWhileRunning()
        {
            Fixture fixture = CreateFixture();

            fixture.Manager.BeginLevelSession(1);

            Assert.That(
                fixture.Manager.StepOneTick(),
                Is.False);

            TowerNodeId generator =
                fixture.Manager.RegisterTower(
                    fixture.Generator,
                    Position(0f));

            TowerNodeId nexus =
                fixture.Manager.RegisterTower(
                    fixture.Nexus,
                    Position(1f));

            fixture.Manager.TryRewire(
                generator,
                nexus,
                out _);

            fixture.Manager.TryStartSimulation(
                out _);

            Assert.That(
                fixture.Manager.StepOneTick(),
                Is.True);

            Assert.That(
                fixture.Manager.StepOneTick(),
                Is.True);

            Assert.That(
                fixture.Manager.CurrentTick,
                Is.EqualTo(2));

            fixture.Manager.StopSimulation();

            Assert.That(
                fixture.Manager.IsRunning,
                Is.False);

            Assert.That(
                fixture.Manager.CurrentTick,
                Is.Zero);

            Assert.That(
                fixture.Manager.StepOneTick(),
                Is.False);
        }
        [Test]
        public void StartingRunningSimulation_IsIdempotent()
        {
            Fixture fixture = CreateFixture();

            fixture.Manager.BeginLevelSession(1);

            TowerNodeId generator =
                fixture.Manager.RegisterTower(
                    fixture.Generator,
                    Position(0f));

            TowerNodeId nexus =
                fixture.Manager.RegisterTower(
                    fixture.Nexus,
                    Position(1f));

            fixture.Manager.TryRewire(
                generator,
                nexus,
                out _);

            fixture.Manager.TryStartSimulation(
                out _);

            fixture.Manager.StepOneTick();

            Assert.That(
                fixture.Manager.TryStartSimulation(
                    out string error),
                Is.True,
                error);

            Assert.That(
                fixture.Manager.CurrentTick,
                Is.EqualTo(1));
        }
        [Test]
        public void RunningSimulation_LocksTopology()
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

            fixture.Manager.TryRewire(
                generator,
                nexus,
                out _);

            fixture.Manager.TryStartSimulation(
                out _);

            Assert.That(
                fixture.Manager.TryRewire(
                    generator,
                    fire,
                    out string error),
                Is.False);

            Assert.That(
                error,
                Does.Contain("running"));
        }

        [Test]
        public void GeneratorWithoutNexus_IsIncomplete()
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

            fixture.Manager.TryRewire(
                generator,
                fire,
                out _);

            Assert.That(
                fixture.Manager.ValidChainCount,
                Is.Zero);
            Assert.That(
                fixture.Manager.HasValidChain,
                Is.False);
            Assert.That(
                fixture.Manager
                    .CreateValidNodeIdSnapshot(),
                Is.Empty);
        }

        [Test]
        public void ElementRoute_MarksEveryRouteNodeValid()
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

            TowerNodeId water =
                fixture.Manager.RegisterTower(
                    fixture.Water,
                    Position(2f));

            TowerNodeId nexus =
                fixture.Manager.RegisterTower(
                    fixture.Nexus,
                    Position(3f));

            fixture.Manager.TryRewire(
                generator,
                fire,
                out _);

            fixture.Manager.TryRewire(
                fire,
                water,
                out _);

            fixture.Manager.TryRewire(
                water,
                nexus,
                out _);

            Assert.That(
                fixture.Manager.ValidChainCount,
                Is.EqualTo(1));

            CollectionAssert.AreEqual(
                new[]
                {
                    generator,
                    fire,
                    water,
                    nexus
                },
                fixture.Manager
                    .CreateValidNodeIdSnapshot());
        }

        [Test]
        public void ProcessorToNexus_WithoutGeneratorIsNotAChain()
        {
            Fixture fixture = CreateFixture();
            fixture.Manager.BeginLevelSession(1);

            TowerNodeId fire =
                fixture.Manager.RegisterTower(
                    fixture.Fire,
                    Position(0f));

            TowerNodeId nexus =
                fixture.Manager.RegisterTower(
                    fixture.Nexus,
                    Position(1f));

            fixture.Manager.TryRewire(
                fire,
                nexus,
                out _);

            Assert.That(
                fixture.Manager.ValidChainCount,
                Is.Zero);
            Assert.That(
                fixture.Manager.IsNodeInValidChain(fire),
                Is.False);
            Assert.That(
                fixture.Manager.IsNodeInValidChain(nexus),
                Is.False);
        }

        [Test]
        public void TwoGeneratorsSharingNexus_CreateTwoChains()
        {
            Fixture fixture = CreateFixture();
            fixture.Manager.BeginLevelSession(1);

            TowerNodeId generatorA =
                fixture.Manager.RegisterTower(
                    fixture.Generator,
                    Position(0f));

            TowerNodeId generatorB =
                fixture.Manager.RegisterTower(
                    fixture.Generator,
                    Position(1f));

            TowerNodeId nexus =
                fixture.Manager.RegisterTower(
                    fixture.Nexus,
                    Position(2f));

            fixture.Manager.TryRewire(
                generatorA,
                nexus,
                out _);

            fixture.Manager.TryRewire(
                generatorB,
                nexus,
                out _);

            Assert.That(
                fixture.Manager.ValidChainCount,
                Is.EqualTo(2));

            Assert.That(
                fixture.Manager.ValidNodeCount,
                Is.EqualTo(3));

            CollectionAssert.AreEqual(
                new[]
                {
                    generatorA,
                    generatorB,
                    nexus
                },
                fixture.Manager
                    .CreateValidNodeIdSnapshot());
        }

        [Test]
        public void DisconnectedTower_IsNotMarkedValid()
        {
            Fixture fixture = CreateFixture();
            fixture.Manager.BeginLevelSession(1);

            TowerNodeId generator =
                fixture.Manager.RegisterTower(
                    fixture.Generator,
                    Position(0f));

            TowerNodeId earth =
                fixture.Manager.RegisterTower(
                    fixture.Earth,
                    Position(1f));

            TowerNodeId nexus =
                fixture.Manager.RegisterTower(
                    fixture.Nexus,
                    Position(2f));

            fixture.Manager.TryRewire(
                generator,
                nexus,
                out _);

            Assert.That(
                fixture.Manager.IsNodeInValidChain(generator),
                Is.True);
            Assert.That(
                fixture.Manager.IsNodeInValidChain(nexus),
                Is.True);
            Assert.That(
                fixture.Manager.IsNodeInValidChain(earth),
                Is.False);
        }

        [Test]
        public void RewireToIncompleteRoute_InvalidatesOldChain()
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

            TowerNodeId water =
                fixture.Manager.RegisterTower(
                    fixture.Water,
                    Position(2f));

            TowerNodeId nexus =
                fixture.Manager.RegisterTower(
                    fixture.Nexus,
                    Position(3f));

            fixture.Manager.TryRewire(
                generator,
                fire,
                out _);

            fixture.Manager.TryRewire(
                fire,
                nexus,
                out _);

            Assert.That(
                fixture.Manager.ValidChainCount,
                Is.EqualTo(1));

            fixture.Manager.TryRewire(
                fire,
                water,
                out _);

            Assert.That(
                fixture.Manager.ValidChainCount,
                Is.Zero);
            Assert.That(
                fixture.Manager
                    .CreateValidNodeIdSnapshot(),
                Is.Empty);
        }

        [Test]
        public void UnlinkOrUnregister_RebuildsValidity()
        {
            Fixture fixture = CreateFixture();
            fixture.Manager.BeginLevelSession(1);

            TowerNodeId generator =
                fixture.Manager.RegisterTower(
                    fixture.Generator,
                    Position(0f));

            TowerNodeId nexus =
                fixture.Manager.RegisterTower(
                    fixture.Nexus,
                    Position(1f));

            fixture.Manager.TryRewire(
                generator,
                nexus,
                out _);

            Assert.That(
                fixture.Manager.ValidChainCount,
                Is.EqualTo(1));

            fixture.Manager.TryUnlinkAll(
                nexus,
                out _);

            Assert.That(
                fixture.Manager.ValidChainCount,
                Is.Zero);

            fixture.Manager.TryRewire(
                generator,
                nexus,
                out _);

            Assert.That(
                fixture.Manager.ValidChainCount,
                Is.EqualTo(1));

            fixture.Manager.UnregisterTower(nexus);

            Assert.That(
                fixture.Manager.ValidChainCount,
                Is.Zero);
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
                water,
                earth,
                nexus);
        }

        private T Create<T>() where T : ScriptableObject
        {
            T value =
                ScriptableObject.CreateInstance<T>();

            owned.Add(value);
            return value;
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

            Assert.That(field, Is.Not.Null);
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
                WaterTowerDefinition water,
                EarthTowerDefinition earth,
                SoulNexusDefinition nexus)
            {
                Manager = manager;
                Generator = generator;
                Fire = fire;
                Water = water;
                Earth = earth;
                Nexus = nexus;
            }

            public TowerNetworkManager Manager { get; }
            public GeneratorTowerDefinition Generator { get; }
            public FireTowerDefinition Fire { get; }
            public WaterTowerDefinition Water { get; }
            public EarthTowerDefinition Earth { get; }
            public SoulNexusDefinition Nexus { get; }
        }
    }
}
