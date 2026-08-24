using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace TowerDefense3D.Towers.Tests.EditMode
{
    public sealed class TowerNetworkManagerSoulNexusTests
    {
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = 0; index < owned.Count; index++)
            {
                UnityEngine.Object.DestroyImmediate(owned[index]);
            }

            owned.Clear();
        }

        [Test]
        public void NexusRuntimeSpec_UsesAuthoredConsumptionContract()
        {
            NexusFixture fixture = CreateRunningFixture();
            TowerRuntimeSpec nexusSpec = GetNodeSpec(fixture.Manager, fixture.NexusId);

            Assert.That(nexusSpec.NetworkRole, Is.EqualTo(TowerNetworkRole.Sink));
            Assert.That(nexusSpec.InputPortCount, Is.EqualTo(2));
            Assert.That(nexusSpec.OutputPortCount, Is.Zero);
            Assert.That(nexusSpec.QueueCapacityPerInput, Is.EqualTo(4));
            Assert.That(nexusSpec.CycleTicks, Is.EqualTo(15));
            Assert.That(nexusSpec.OutputProjectileCount, Is.Zero);
            Assert.That(nexusSpec.RequiredDownstreamReservationCount, Is.Zero);
            Assert.That(nexusSpec.ConsumeBatchSize, Is.EqualTo(1));
            Assert.That(
                nexusSpec.ConsumeOrder,
                Is.EqualTo(SoulConsumeOrder.OldestArrivalThenInputPortThenProjectileId));
        }

        [Test]
        public void Nexus_WaitsFullConsumeCycleFromArrivalTick()
        {
            NexusFixture fixture = CreateRunningFixture();

            StepTicks(fixture.Manager, 22);

            TowerNodeSimulationSnapshot firstConsumeTick =
                GetNodeSnapshot(fixture.Manager, fixture.NexusId);

            TowerInputPortSnapshot firstInput =
                GetInputSnapshot(fixture.Manager, fixture.NexusId, inputPort: 0);

            Assert.That(fixture.Manager.CurrentTick, Is.EqualTo(22));
            Assert.That(firstConsumeTick.CycleTicks, Is.EqualTo(15));
            Assert.That(firstConsumeTick.CycleProgressTicks, Is.EqualTo(1));
            Assert.That(firstConsumeTick.IsReady, Is.False);
            Assert.That(firstInput.QueuedProjectileCount, Is.EqualTo(1));
            Assert.That(firstInput.ReservedProjectileCount, Is.Zero);

            StepTicks(fixture.Manager, 13);

            TowerNodeSimulationSnapshot beforeConsume =
                GetNodeSnapshot(fixture.Manager, fixture.NexusId);

            Assert.That(fixture.Manager.CurrentTick, Is.EqualTo(35));
            Assert.That(beforeConsume.CycleProgressTicks, Is.EqualTo(14));
            Assert.That(beforeConsume.RemainingCycleTicks, Is.EqualTo(1));
            Assert.That(GetInputSnapshot(fixture.Manager, fixture.NexusId, 0).QueuedProjectileCount, Is.EqualTo(1));

            Assert.That(fixture.Manager.StepOneTick(), Is.True);

            TowerNodeSimulationSnapshot afterConsume =
                GetNodeSnapshot(fixture.Manager, fixture.NexusId);

            TowerInputPortSnapshot consumedInput =
                GetInputSnapshot(fixture.Manager, fixture.NexusId, inputPort: 0);

            Assert.That(fixture.Manager.CurrentTick, Is.EqualTo(36));
            Assert.That(afterConsume.CycleProgressTicks, Is.Zero);
            Assert.That(afterConsume.RemainingCycleTicks, Is.EqualTo(15));
            Assert.That(afterConsume.IsReady, Is.False);
            Assert.That(consumedInput.QueuedProjectileCount, Is.Zero);
            Assert.That(consumedInput.ReservedProjectileCount, Is.Zero);
            Assert.That(fixture.Manager.TryPeekInputProjectile(fixture.NexusId, 0, out _), Is.False);
        }

        [Test]
        public void Nexus_OlderArrivalWinsBeforeLowerInputPort()
        {
            NexusFixture fixture = CreateRunningFixture(
                firstGeneratorX: 0f,
                nexusX: 2f,
                includeSecondGenerator: true,
                secondGeneratorX: 1.5f);

            StepTicks(fixture.Manager, 35);

            TowerInputPortSnapshot lowerPort =
                GetInputSnapshot(fixture.Manager, fixture.NexusId, inputPort: 0);

            TowerInputPortSnapshot higherPort =
                GetInputSnapshot(fixture.Manager, fixture.NexusId, inputPort: 1);

            Assert.That(fixture.Manager.CurrentTick, Is.EqualTo(35));
            Assert.That(lowerPort.QueuedProjectileCount, Is.EqualTo(1));
            Assert.That(higherPort.QueuedProjectileCount, Is.Zero);

            Assert.That(
                fixture.Manager.TryPeekInputProjectile(
                    fixture.NexusId, 0, out ProjectileQueueEntry remainingLowerPortInput),
                Is.True);

            Assert.That(remainingLowerPortInput.ProjectileId, Is.EqualTo(1L));
            Assert.That(remainingLowerPortInput.ArrivalTick, Is.EqualTo(24L));
            Assert.That(fixture.Manager.TryPeekInputProjectile(fixture.NexusId, 1, out _), Is.False);

            TowerNodeSimulationSnapshot nexus =
                GetNodeSnapshot(fixture.Manager, fixture.NexusId);

            Assert.That(nexus.CycleProgressTicks, Is.Zero);
        }

        [Test]
        public void Nexus_SameArrivalUsesLowerInputPort()
        {
            NexusFixture fixture = CreateRunningFixture(
                firstGeneratorX: 0f,
                nexusX: 1f,
                includeSecondGenerator: true,
                secondGeneratorX: 0f);

            StepTicks(fixture.Manager, 36);

            TowerInputPortSnapshot lowerPort =
                GetInputSnapshot(fixture.Manager, fixture.NexusId, inputPort: 0);

            TowerInputPortSnapshot higherPort =
                GetInputSnapshot(fixture.Manager, fixture.NexusId, inputPort: 1);

            Assert.That(lowerPort.QueuedProjectileCount, Is.Zero);
            Assert.That(higherPort.QueuedProjectileCount, Is.EqualTo(1));
            Assert.That(fixture.Manager.TryPeekInputProjectile(fixture.NexusId, 0, out _), Is.False);

            Assert.That(
                fixture.Manager.TryPeekInputProjectile(
                    fixture.NexusId, 1, out ProjectileQueueEntry remainingHigherPortInput),
                Is.True);

            Assert.That(remainingHigherPortInput.ProjectileId, Is.EqualTo(2L));
            Assert.That(remainingHigherPortInput.ArrivalTick, Is.EqualTo(22L));

            TowerNodeSimulationSnapshot nexus =
                GetNodeSnapshot(fixture.Manager, fixture.NexusId);

            Assert.That(nexus.CycleProgressTicks, Is.Zero);
        }

        [Test]
        public void Nexus_EmptyLowerInputDoesNotBlockHigherInput()
        {
            NexusFixture fixture = CreateRunningFixture(
                firstGeneratorX: 0f,
                nexusX: 12f,
                includeSecondGenerator: true,
                secondGeneratorX: 11.5f);

            StepTicks(fixture.Manager, 35);

            TowerInputPortSnapshot lowerPort =
                GetInputSnapshot(fixture.Manager, fixture.NexusId, inputPort: 0);

            TowerInputPortSnapshot higherPort =
                GetInputSnapshot(fixture.Manager, fixture.NexusId, inputPort: 1);

            Assert.That(fixture.Manager.CurrentTick, Is.EqualTo(35));
            Assert.That(fixture.Manager.ProjectileCount, Is.EqualTo(1));
            Assert.That(lowerPort.QueuedProjectileCount, Is.Zero);
            Assert.That(lowerPort.ReservedProjectileCount, Is.EqualTo(1));
            Assert.That(higherPort.QueuedProjectileCount, Is.Zero);
            Assert.That(higherPort.ReservedProjectileCount, Is.Zero);
            Assert.That(fixture.Manager.TryPeekInputProjectile(fixture.NexusId, 0, out _), Is.False);
            Assert.That(fixture.Manager.TryPeekInputProjectile(fixture.NexusId, 1, out _), Is.False);

            TowerNodeSimulationSnapshot nexus =
                GetNodeSnapshot(fixture.Manager, fixture.NexusId);

            Assert.That(nexus.CycleProgressTicks, Is.Zero);
        }

        [Test]
        public void StopSimulation_ClearsNexusQueuesAndCycleProgress()
        {
            NexusFixture fixture = CreateRunningFixture();

            StepTicks(fixture.Manager, 22);

            Assert.That(GetInputSnapshot(fixture.Manager, fixture.NexusId, 0).QueuedProjectileCount, Is.EqualTo(1));
            Assert.That(GetNodeSnapshot(fixture.Manager, fixture.NexusId).CycleProgressTicks, Is.EqualTo(1));

            fixture.Manager.StopSimulation();

            TowerNodeSimulationSnapshot stoppedNexus =
                GetNodeSnapshot(fixture.Manager, fixture.NexusId);

            TowerInputPortSnapshot firstInput =
                GetInputSnapshot(fixture.Manager, fixture.NexusId, inputPort: 0);

            TowerInputPortSnapshot secondInput =
                GetInputSnapshot(fixture.Manager, fixture.NexusId, inputPort: 1);

            Assert.That(fixture.Manager.IsRunning, Is.False);
            Assert.That(fixture.Manager.CurrentTick, Is.Zero);
            Assert.That(fixture.Manager.ProjectileCount, Is.Zero);
            Assert.That(stoppedNexus.CycleProgressTicks, Is.Zero);
            Assert.That(firstInput.QueuedProjectileCount, Is.Zero);
            Assert.That(firstInput.ReservedProjectileCount, Is.Zero);
            Assert.That(secondInput.QueuedProjectileCount, Is.Zero);
            Assert.That(secondInput.ReservedProjectileCount, Is.Zero);
        }

        [Test]
        public void SinkRuntimeSpec_RejectsMissingConsumptionContract()
        {
            ProjectilePayload emptyPayload =
                new ProjectilePayload(ProjectilePayloadKind.Physical, 0f, DamageType.Physical);

            Assert.Throws<ArgumentException>(
                () => new TowerRuntimeSpec(
                    TowerFamily.SoulNexus,
                    TowerNetworkRole.Sink,
                    "soul_nexus",
                    2,
                    0,
                    4,
                    15,
                    0,
                    0,
                    0,
                    emptyPayload));
        }

        private NexusFixture CreateRunningFixture(
            float firstGeneratorX = 0f, float nexusX = 1f,
            bool includeSecondGenerator = false, float secondGeneratorX = 0f)
        {
            TowerCombatRules rules = Create<TowerCombatRules>();
            GeneratorTowerDefinition generator = Create<GeneratorTowerDefinition>();
            FireTowerDefinition fire = Create<FireTowerDefinition>();
            WaterTowerDefinition water = Create<WaterTowerDefinition>();
            WindTowerDefinition wind = Create<WindTowerDefinition>();
            EarthTowerDefinition earth = Create<EarthTowerDefinition>();
            SoulNexusDefinition nexus = Create<SoulNexusDefinition>();
            TowerCatalog catalog = Create<TowerCatalog>();

            SetPrivateField(catalog, "combatRules", rules);
            SetPrivateField(
                catalog,
                "definitions",
                new List<TowerCombatDefinition> { generator, fire, water, wind, earth, nexus });

            TowerNetworkManager manager = new TowerNetworkManager(catalog);
            manager.BeginLevelSession(1);

            TowerNodeId firstGeneratorId =
                manager.RegisterTower(generator, Position(firstGeneratorX));

            TowerNodeId secondGeneratorId = default;

            if (includeSecondGenerator)
            {
                secondGeneratorId =
                    manager.RegisterTower(generator, Position(secondGeneratorX));
            }

            TowerNodeId nexusId = manager.RegisterTower(nexus, Position(nexusX));

            Assert.That(
                manager.TryRewire(firstGeneratorId, nexusId, out string firstLinkError),
                Is.True,
                firstLinkError);

            if (includeSecondGenerator)
            {
                Assert.That(
                    manager.TryRewire(secondGeneratorId, nexusId, out string secondLinkError),
                    Is.True,
                    secondLinkError);
            }

            Assert.That(manager.TryStartSimulation(out string startError), Is.True, startError);

            return new NexusFixture(manager, firstGeneratorId, secondGeneratorId, nexusId);
        }

        private static TowerNodeSimulationSnapshot GetNodeSnapshot(
            TowerNetworkManager manager, TowerNodeId nodeId)
        {
            Assert.That(
                manager.TryCreateNodeSimulationSnapshot(
                    nodeId, out TowerNodeSimulationSnapshot snapshot),
                Is.True);

            return snapshot;
        }

        private static TowerInputPortSnapshot GetInputSnapshot(
            TowerNetworkManager manager, TowerNodeId nodeId, int inputPort)
        {
            Assert.That(
                manager.TryCreateInputPortSnapshot(
                    nodeId, inputPort, out TowerInputPortSnapshot snapshot),
                Is.True);

            return snapshot;
        }

        private static TowerRuntimeSpec GetNodeSpec(
            TowerNetworkManager manager, TowerNodeId nodeId)
        {
            Assert.That(manager.TryGetNodeSpec(nodeId, out TowerRuntimeSpec spec), Is.True);
            return spec;
        }

        private static void StepTicks(TowerNetworkManager manager, int tickCount)
        {
            for (int tick = 0; tick < tickCount; tick++)
            {
                Assert.That(manager.StepOneTick(), Is.True);
            }
        }

        private T Create<T>() where T : ScriptableObject
        {
            T value = ScriptableObject.CreateInstance<T>();
            owned.Add(value);
            return value;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }

        private static TowerWorldPosition Position(float x)
        {
            return new TowerWorldPosition(x, 0f, 0f);
        }

        private readonly struct NexusFixture
        {
            public NexusFixture(
                TowerNetworkManager manager, TowerNodeId firstGeneratorId,
                TowerNodeId secondGeneratorId, TowerNodeId nexusId)
            {
                Manager = manager;
                FirstGeneratorId = firstGeneratorId;
                SecondGeneratorId = secondGeneratorId;
                NexusId = nexusId;
            }

            public TowerNetworkManager Manager { get; }
            public TowerNodeId FirstGeneratorId { get; }
            public TowerNodeId SecondGeneratorId { get; }
            public TowerNodeId NexusId { get; }
        }
    }
}