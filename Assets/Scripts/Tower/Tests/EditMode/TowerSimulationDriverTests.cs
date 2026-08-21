using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace TowerDefense3D.Towers.Tests.EditMode
{
    public sealed class TowerSimulationDriverTests
    {
        private readonly List<UnityEngine.Object> owned =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = 0;
                 index < owned.Count;
                 index++)
            {
                UnityEngine.Object.DestroyImmediate(
                    owned[index]);
            }

            owned.Clear();
        }

        [Test]
        public void AdvanceFrame_CarriesRemainderBetweenFrames()
        {
            Fixture fixture = CreateRunningFixture();

            TowerSimulationDriver driver =
                CreateDriver(fixture.Manager);

            int firstFrameTicks =
                driver.AdvanceFrame(0.03f);

            Assert.That(
                firstFrameTicks,
                Is.Zero);

            Assert.That(
                fixture.Manager.CurrentTick,
                Is.Zero);

            int secondFrameTicks =
                driver.AdvanceFrame(0.03f);

            Assert.That(
                secondFrameTicks,
                Is.EqualTo(1));

            Assert.That(
                fixture.Manager.CurrentTick,
                Is.EqualTo(1));

            Assert.That(
                driver.AccumulatedSeconds,
                Is.EqualTo(0.01d)
                    .Within(0.000001d));
        }

        [Test]
        public void LongFrame_ExecutesEveryCrossedTick()
        {
            Fixture fixture = CreateRunningFixture();

            TowerSimulationDriver driver =
                CreateDriver(fixture.Manager);

            int executedTicks =
                driver.AdvanceFrame(0.21f);

            Assert.That(
                executedTicks,
                Is.EqualTo(4));

            Assert.That(
                fixture.Manager.CurrentTick,
                Is.EqualTo(4));

            Assert.That(
                driver.AccumulatedSeconds,
                Is.EqualTo(0.01d)
                    .Within(0.000001d));
        }

        [Test]
        public void StoppedSimulation_DoesNotAccumulateTime()
        {
            Fixture fixture = CreateFixture();

            TowerSimulationDriver driver =
                CreateDriver(fixture.Manager);

            int executedTicks =
                driver.AdvanceFrame(1f);

            Assert.That(
                executedTicks,
                Is.Zero);

            Assert.That(
                driver.AccumulatedSeconds,
                Is.Zero);

            Assert.That(
                fixture.Manager.CurrentTick,
                Is.Zero);
        }

        [Test]
        public void Shutdown_ClearsDriverState()
        {
            Fixture fixture = CreateRunningFixture();

            TowerSimulationDriver driver =
                CreateDriver(fixture.Manager);

            driver.AdvanceFrame(0.03f);

            driver.Shutdown();

            Assert.That(
                driver.IsInitialized,
                Is.False);

            Assert.That(
                driver.AccumulatedSeconds,
                Is.Zero);

            Assert.Throws<InvalidOperationException>(
                () => driver.AdvanceFrame(0.05f));
        }

        [Test]
        public void AdvanceFrame_RejectsInvalidDeltaTime()
        {
            Fixture fixture = CreateRunningFixture();

            TowerSimulationDriver driver =
                CreateDriver(fixture.Manager);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => driver.AdvanceFrame(-0.01f));

            Assert.Throws<ArgumentOutOfRangeException>(
                () => driver.AdvanceFrame(
                    float.NaN));

            Assert.Throws<ArgumentOutOfRangeException>(
                () => driver.AdvanceFrame(
                    float.PositiveInfinity));
        }

        private TowerSimulationDriver CreateDriver(
            TowerNetworkManager manager)
        {
            var gameObject =
                new GameObject(
                    "Tower Simulation Driver Test");

            owned.Add(gameObject);

            TowerSimulationDriver driver =
                gameObject.AddComponent<
                    TowerSimulationDriver>();

            driver.Initialize(manager);

            return driver;
        }

        private Fixture CreateRunningFixture()
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

            return fixture;
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
                nexus);
        }

        private T Create<T>()
            where T : ScriptableObject
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

            Assert.That(
                field,
                Is.Not.Null,
                $"Missing field '{fieldName}'.");

            field.SetValue(target, value);
        }

        private static TowerWorldPosition Position(
            float x)
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
                SoulNexusDefinition nexus)
            {
                Manager = manager;
                Generator = generator;
                Nexus = nexus;
            }

            public TowerNetworkManager Manager { get; }

            public GeneratorTowerDefinition Generator { get; }

            public SoulNexusDefinition Nexus { get; }
        }
    }
}