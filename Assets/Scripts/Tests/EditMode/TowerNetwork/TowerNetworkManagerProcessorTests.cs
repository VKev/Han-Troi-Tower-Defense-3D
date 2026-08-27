using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace TowerDefense3D.Towers.Tests.EditMode
{
    public sealed class TowerNetworkManagerProcessorTests
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
        public void Processor_DoesNotAdvanceBeforeInputArrives()
        {
            ProcessorFixture fixture = CreateRunningFixture(TowerFamily.Fire);

            StepTicks(fixture.Manager, 21);

            TowerNodeSimulationSnapshot beforeArrival = GetNodeSnapshot(fixture.Manager, fixture.ProcessorId);
            TowerInputPortSnapshot inputBeforeArrival =
                GetInputSnapshot(fixture.Manager, fixture.ProcessorId, inputPort: 0);

            Assert.That(fixture.Manager.CurrentTick, Is.EqualTo(21));
            Assert.That(fixture.Manager.ProjectileCount, Is.EqualTo(1));
            Assert.That(beforeArrival.CycleProgressTicks, Is.Zero);
            Assert.That(beforeArrival.IsReady, Is.False);
            Assert.That(inputBeforeArrival.QueuedProjectileCount, Is.Zero);
            Assert.That(inputBeforeArrival.ReservedProjectileCount, Is.EqualTo(1));

            Assert.That(fixture.Manager.StepOneTick(), Is.True);

            TowerNodeSimulationSnapshot afterArrival = GetNodeSnapshot(fixture.Manager, fixture.ProcessorId);
            TowerInputPortSnapshot inputAfterArrival =
                GetInputSnapshot(fixture.Manager, fixture.ProcessorId, inputPort: 0);

            Assert.That(fixture.Manager.CurrentTick, Is.EqualTo(22));
            Assert.That(fixture.Manager.ProjectileCount, Is.Zero);
            Assert.That(afterArrival.CycleProgressTicks, Is.EqualTo(1));
            Assert.That(afterArrival.IsReady, Is.False);
            Assert.That(inputAfterArrival.QueuedProjectileCount, Is.EqualTo(1));
            Assert.That(inputAfterArrival.ReservedProjectileCount, Is.Zero);

            Assert.That(
                fixture.Manager.TryPeekInputProjectile(
                    fixture.ProcessorId, 0, out ProjectileQueueEntry queuedInput),
                Is.True);

            Assert.That(queuedInput.ProjectileId, Is.EqualTo(1L));
            Assert.That(queuedInput.ArrivalTick, Is.EqualTo(22L));
            Assert.That(queuedInput.Payload.Kind, Is.EqualTo(ProjectilePayloadKind.Physical));
            Assert.That(queuedInput.Payload.DamageType, Is.EqualTo(DamageType.Physical));
        }

        [TestCase(TowerFamily.Fire, ProjectilePayloadKind.Fire, DamageType.Magic, 1)]
        [TestCase(TowerFamily.Water, ProjectilePayloadKind.Water, DamageType.Magic, 1)]
        [TestCase(TowerFamily.Wind, ProjectilePayloadKind.Wind, DamageType.Magic, 1)]
        [TestCase(TowerFamily.Earth, ProjectilePayloadKind.Earth, DamageType.Physical, 1)]
        public void ElementProcessor_ReplacesIncomingPayload(
            TowerFamily processorFamily, ProjectilePayloadKind expectedKind,
            DamageType expectedDamageType, int expectedOutputCount)
        {
            ProcessorFixture fixture = CreateRunningFixture(processorFamily);

            StepTicks(fixture.Manager, 22);

            Assert.That(
                fixture.Manager.TryPeekInputProjectile(
                    fixture.ProcessorId, 0, out ProjectileQueueEntry incomingProjectile),
                Is.True);

            Assert.That(incomingProjectile.ProjectileId, Is.EqualTo(1L));
            Assert.That(incomingProjectile.Payload.Kind, Is.EqualTo(ProjectilePayloadKind.Physical));
            Assert.That(incomingProjectile.Payload.DamageType, Is.EqualTo(DamageType.Physical));

            TowerRuntimeSpec processorSpec = GetNodeSpec(fixture.Manager, fixture.ProcessorId);

            StepTicks(fixture.Manager, processorSpec.CycleTicks - 1);

            IReadOnlyList<TowerProjectileSnapshot> outputProjectiles =
                fixture.Manager.CreateProjectileSnapshot();

            Assert.That(outputProjectiles.Count, Is.EqualTo(expectedOutputCount));

            for (int index = 0; index < outputProjectiles.Count; index++)
            {
                TowerProjectileSnapshot output = outputProjectiles[index];

                Assert.That(output.ProjectileId, Is.Not.EqualTo(incomingProjectile.ProjectileId));
                Assert.That(output.Source, Is.EqualTo(fixture.ProcessorId));
                Assert.That(output.Target, Is.EqualTo(fixture.NexusId));
                Assert.That(output.Position.X, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(output.Position.Y, Is.Zero);
                Assert.That(output.Position.Z, Is.Zero);
                Assert.That(output.Payload.Kind, Is.EqualTo(expectedKind));
                Assert.That(output.Payload.DamageType, Is.EqualTo(expectedDamageType));
                Assert.That(
                    output.Payload.Damage,
                    Is.EqualTo(processorSpec.OutputPayload.Damage).Within(0.0001f));
            }

            TowerNodeSimulationSnapshot processor =
                GetNodeSnapshot(fixture.Manager, fixture.ProcessorId);

            TowerInputPortSnapshot nexusInput =
                GetInputSnapshot(fixture.Manager, fixture.NexusId, inputPort: 0);

            Assert.That(processor.CycleProgressTicks, Is.Zero);
            Assert.That(processor.IsReady, Is.False);
            Assert.That(nexusInput.QueuedProjectileCount, Is.Zero);
            Assert.That(nexusInput.ReservedProjectileCount, Is.EqualTo(expectedOutputCount));

            bool hasRemainingInput = fixture.Manager.TryPeekInputProjectile(
                fixture.ProcessorId, 0, out ProjectileQueueEntry remainingInput);

            if (hasRemainingInput)
            {
                Assert.That(remainingInput.ProjectileId, Is.Not.EqualTo(incomingProjectile.ProjectileId));
            }
        }

        [Test]
        public void FireProcessor_EmitsSingleProjectile()
        {
            ProcessorFixture fixture = CreateRunningFixture(TowerFamily.Fire);

            StepTicks(fixture.Manager, 38);

            TowerRuntimeSpec fireSpec = GetNodeSpec(fixture.Manager, fixture.ProcessorId);
            IReadOnlyList<TowerProjectileSnapshot> projectiles =
                fixture.Manager.CreateProjectileSnapshot();

            Assert.That(fireSpec.CycleTicks, Is.EqualTo(17));
            Assert.That(fireSpec.OutputProjectileCount, Is.EqualTo(1));
            Assert.That(fireSpec.RequiredDownstreamReservationCount, Is.EqualTo(1));
            Assert.That(fireSpec.SequenceSpacingTicks, Is.Zero);
            Assert.That(projectiles.Count, Is.EqualTo(1));

            TowerProjectileSnapshot projectile = projectiles[0];

            Assert.That(projectile.ProjectileId, Is.EqualTo(2L));
            Assert.That(projectile.Source, Is.EqualTo(fixture.ProcessorId));
            Assert.That(projectile.Target, Is.EqualTo(fixture.NexusId));
            Assert.That(projectile.Position.X, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(projectile.LaunchDelayTicks, Is.Zero);
            Assert.That(projectile.Payload.Kind, Is.EqualTo(ProjectilePayloadKind.Fire));
            Assert.That(projectile.Payload.DamageType, Is.EqualTo(DamageType.Magic));
            Assert.That(projectile.Payload.Damage, Is.EqualTo(6f).Within(0.0001f));

            TowerInputPortSnapshot fireInput =
                GetInputSnapshot(fixture.Manager, fixture.ProcessorId, inputPort: 0);

            TowerInputPortSnapshot nexusInput =
                GetInputSnapshot(fixture.Manager, fixture.NexusId, inputPort: 0);

            TowerNodeSimulationSnapshot fire =
                GetNodeSnapshot(fixture.Manager, fixture.ProcessorId);

            Assert.That(fireInput.QueuedProjectileCount, Is.Zero);
            Assert.That(fireInput.ReservedProjectileCount, Is.Zero);
            Assert.That(nexusInput.Capacity, Is.EqualTo(4));
            Assert.That(nexusInput.QueuedProjectileCount, Is.Zero);
            Assert.That(nexusInput.ReservedProjectileCount, Is.EqualTo(1));
            Assert.That(nexusInput.AvailableSlotCount, Is.EqualTo(3));
            Assert.That(fire.CycleProgressTicks, Is.Zero);
            Assert.That(fire.IsReady, Is.False);
        }

        [Test]
        public void BlockedFireProcessor_KeepsInputAndReady()
        {
            // Fire emits one projectile per cycle, which the default nexus consumes faster than
            // the chain produces. Starve the sink so the downstream port can actually fill up.
            ProcessorFixture fixture = CreateRunningFixture(TowerFamily.Fire, nexusCycleSeconds: 60f);

            TowerInputPortSnapshot nexusInput = default;
            TowerNodeSimulationSnapshot blockedFire = default;
            TowerInputPortSnapshot blockedFireInput = default;
            bool isBlocked = false;
            for (int step = 0; step < 500 && !isBlocked; step++)
            {
                Assert.That(fixture.Manager.StepOneTick(), Is.True);
                nexusInput = GetInputSnapshot(fixture.Manager, fixture.NexusId, inputPort: 0);
                blockedFire = GetNodeSnapshot(fixture.Manager, fixture.ProcessorId);
                blockedFireInput =
                    GetInputSnapshot(fixture.Manager, fixture.ProcessorId, inputPort: 0);
                isBlocked = nexusInput.AvailableSlotCount == 0
                    && blockedFire.IsReady
                    && blockedFireInput.QueuedProjectileCount > 0;
            }

            Assert.That(isBlocked, Is.True, "Fire processor never reached the blocked state.");
            Assert.That(nexusInput.Capacity, Is.EqualTo(4));
            Assert.That(nexusInput.OccupiedSlotCount, Is.EqualTo(nexusInput.Capacity));
            Assert.That(blockedFire.CycleProgressTicks, Is.EqualTo(blockedFire.CycleTicks));
            Assert.That(blockedFire.RemainingCycleTicks, Is.Zero);

            Assert.That(
                fixture.Manager.TryPeekInputProjectile(
                    fixture.ProcessorId, 0, out ProjectileQueueEntry blockedInput),
                Is.True);

            Assert.That(fixture.Manager.StepOneTick(), Is.True);

            TowerNodeSimulationSnapshot retriedFire =
                GetNodeSnapshot(fixture.Manager, fixture.ProcessorId);

            TowerInputPortSnapshot unchangedNexusInput =
                GetInputSnapshot(fixture.Manager, fixture.NexusId, inputPort: 0);

            Assert.That(retriedFire.CycleProgressTicks, Is.EqualTo(retriedFire.CycleTicks));
            Assert.That(retriedFire.IsReady, Is.True);
            Assert.That(unchangedNexusInput.AvailableSlotCount, Is.Zero);

            Assert.That(
                fixture.Manager.TryPeekInputProjectile(
                    fixture.ProcessorId, 0, out ProjectileQueueEntry retainedInput),
                Is.True);

            Assert.That(retainedInput.ProjectileId, Is.EqualTo(blockedInput.ProjectileId));
            Assert.That(retainedInput.ArrivalTick, Is.EqualTo(blockedInput.ArrivalTick));
            Assert.That(retainedInput.Payload.Kind, Is.EqualTo(blockedInput.Payload.Kind));
            Assert.That(retainedInput.Payload.DamageType, Is.EqualTo(blockedInput.Payload.DamageType));
            Assert.That(retainedInput.Payload.Damage, Is.EqualTo(blockedInput.Payload.Damage));
        }

        private ProcessorFixture CreateRunningFixture(
            TowerFamily processorFamily, float nexusCycleSeconds = 0f)
        {
            TowerCombatRules rules = Create<TowerCombatRules>();
            GeneratorTowerDefinition generator = Create<GeneratorTowerDefinition>();
            FireTowerDefinition fire = Create<FireTowerDefinition>();
            WaterTowerDefinition water = Create<WaterTowerDefinition>();
            WindTowerDefinition wind = Create<WindTowerDefinition>();
            EarthTowerDefinition earth = Create<EarthTowerDefinition>();
            SoulNexusDefinition nexus = Create<SoulNexusDefinition>();
            TowerCatalog catalog = Create<TowerCatalog>();

            TowerCombatDefinition processor =
                SelectProcessor(processorFamily, fire, water, wind, earth);

            if (nexusCycleSeconds > 0f)
            {
                SetPrivateField(nexus.Core.Throughput, "cycleIntervalSeconds", nexusCycleSeconds);
            }

            SetPrivateField(catalog, "combatRules", rules);
            SetPrivateField(
                catalog,
                "definitions",
                new List<TowerCombatDefinition> { generator, fire, water, wind, earth, nexus });

            TowerNetworkManager manager = new TowerNetworkManager(catalog);
            manager.BeginLevelSession(1);

            TowerNodeId generatorId = manager.RegisterTower(generator, Position(0f));
            TowerNodeId processorId = manager.RegisterTower(processor, Position(1f));
            TowerNodeId nexusId = manager.RegisterTower(nexus, Position(2f));

            Assert.That(manager.TryRewire(generatorId, processorId, out string firstLinkError), Is.True, firstLinkError);
            Assert.That(manager.TryRewire(processorId, nexusId, out string secondLinkError), Is.True, secondLinkError);
            Assert.That(manager.TryStartSimulation(out string startError), Is.True, startError);

            return new ProcessorFixture(manager, generatorId, processorId, nexusId);
        }

        private static TowerCombatDefinition SelectProcessor(
            TowerFamily processorFamily, FireTowerDefinition fire, WaterTowerDefinition water,
            WindTowerDefinition wind, EarthTowerDefinition earth)
        {
            switch (processorFamily)
            {
                case TowerFamily.Fire:
                    return fire;

                case TowerFamily.Water:
                    return water;

                case TowerFamily.Wind:
                    return wind;

                case TowerFamily.Earth:
                    return earth;

                default:
                    throw new AssertionException(
                        $"Tower family {processorFamily} is not an element processor.");
            }
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
            Assert.That(
                manager.TryGetNodeSpec(nodeId, out TowerRuntimeSpec spec),
                Is.True);

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

        private readonly struct ProcessorFixture
        {
            public ProcessorFixture(
                TowerNetworkManager manager, TowerNodeId generatorId,
                TowerNodeId processorId, TowerNodeId nexusId)
            {
                Manager = manager;
                GeneratorId = generatorId;
                ProcessorId = processorId;
                NexusId = nexusId;
            }

            public TowerNetworkManager Manager { get; }
            public TowerNodeId GeneratorId { get; }
            public TowerNodeId ProcessorId { get; }
            public TowerNodeId NexusId { get; }
        }
    }
}
