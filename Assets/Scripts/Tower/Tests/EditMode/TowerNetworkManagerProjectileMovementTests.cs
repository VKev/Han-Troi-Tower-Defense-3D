using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace TowerDefense3D.Towers.Tests.EditMode
{
    public sealed class TowerNetworkManagerProjectileMovementTests
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
        public void Projectile_MovesBySpeedMultipliedByTickDuration()
        {
            MovementFixture fixture = CreateRunningFixture(targetX: 1f);

            StepTicks(fixture.Manager, 20);

            TowerProjectileSnapshot spawnedProjectile = GetOnlyProjectile(fixture.Manager);

            Assert.That(fixture.Manager.CurrentTick, Is.EqualTo(20));
            Assert.That(spawnedProjectile.Position.X, Is.EqualTo(0f));

            Assert.That(fixture.Manager.StepOneTick(), Is.True);

            TowerProjectileSnapshot movedProjectile = GetOnlyProjectile(fixture.Manager);

            Assert.That(fixture.Manager.CurrentTick, Is.EqualTo(21));
            Assert.That(movedProjectile.Position.X, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(movedProjectile.Position.Y, Is.EqualTo(0f));
            Assert.That(movedProjectile.Position.Z, Is.EqualTo(0f));

            TowerInputPortSnapshot input = GetInputSnapshot(fixture.Manager, fixture.NexusId);

            Assert.That(input.QueuedProjectileCount, Is.Zero);
            Assert.That(input.ReservedProjectileCount, Is.EqualTo(1));
            Assert.That(input.OccupiedSlotCount, Is.EqualTo(1));

            Assert.That(
                fixture.Manager.TryPeekInputProjectile(fixture.NexusId, 0, out _),
                Is.False);
        }

        [Test]
        public void Arrival_ConvertsReservationIntoQueuedProjectile()
        {
            MovementFixture fixture = CreateRunningFixture(targetX: 1f);

            StepTicks(fixture.Manager, 22);

            Assert.That(fixture.Manager.CurrentTick, Is.EqualTo(22));
            Assert.That(fixture.Manager.ProjectileCount, Is.Zero);
            Assert.That(fixture.Manager.CreateProjectileSnapshot(), Is.Empty);

            TowerInputPortSnapshot input = GetInputSnapshot(fixture.Manager, fixture.NexusId);

            Assert.That(input.QueuedProjectileCount, Is.EqualTo(1));
            Assert.That(input.ReservedProjectileCount, Is.Zero);
            Assert.That(input.OccupiedSlotCount, Is.EqualTo(1));
            Assert.That(input.AvailableSlotCount, Is.EqualTo(3));

            Assert.That(
                fixture.Manager.TryPeekInputProjectile(
                    fixture.NexusId, 0, out ProjectileQueueEntry queuedProjectile),
                Is.True);

            Assert.That(queuedProjectile.ProjectileId, Is.EqualTo(1L));
            Assert.That(queuedProjectile.ArrivalTick, Is.EqualTo(22));
            Assert.That(queuedProjectile.Payload.Kind, Is.EqualTo(ProjectilePayloadKind.Physical));
            Assert.That(queuedProjectile.Payload.DamageType, Is.EqualTo(DamageType.Physical));
            Assert.That(queuedProjectile.Payload.Damage, Is.GreaterThan(0f));
        }

        [Test]
        public void Movement_DoesNotOvershootTarget()
        {
            MovementFixture fixture = CreateRunningFixture(targetX: 1.2f);

            StepTicks(fixture.Manager, 20);

            Assert.That(fixture.Manager.StepOneTick(), Is.True);
            Assert.That(GetOnlyProjectile(fixture.Manager).Position.X, Is.EqualTo(0.5f).Within(0.0001f));

            Assert.That(fixture.Manager.StepOneTick(), Is.True);
            Assert.That(GetOnlyProjectile(fixture.Manager).Position.X, Is.EqualTo(1f).Within(0.0001f));

            Assert.That(fixture.Manager.StepOneTick(), Is.True);

            Assert.That(fixture.Manager.CurrentTick, Is.EqualTo(23));
            Assert.That(fixture.Manager.ProjectileCount, Is.Zero);

            TowerInputPortSnapshot input = GetInputSnapshot(fixture.Manager, fixture.NexusId);

            Assert.That(input.QueuedProjectileCount, Is.EqualTo(1));
            Assert.That(input.ReservedProjectileCount, Is.Zero);

            Assert.That(
                fixture.Manager.TryPeekInputProjectile(
                    fixture.NexusId, 0, out ProjectileQueueEntry queuedProjectile),
                Is.True);

            Assert.That(queuedProjectile.ArrivalTick, Is.EqualTo(23));
        }

        [Test]
        public void StopAfterArrival_ClearsQueuedProjectile()
        {
            MovementFixture fixture = CreateRunningFixture(targetX: 1f);

            StepTicks(fixture.Manager, 22);

            TowerInputPortSnapshot beforeStop = GetInputSnapshot(fixture.Manager, fixture.NexusId);

            Assert.That(beforeStop.QueuedProjectileCount, Is.EqualTo(1));
            Assert.That(beforeStop.ReservedProjectileCount, Is.Zero);

            fixture.Manager.StopSimulation();

            TowerInputPortSnapshot afterStop = GetInputSnapshot(fixture.Manager, fixture.NexusId);

            Assert.That(fixture.Manager.IsRunning, Is.False);
            Assert.That(fixture.Manager.CurrentTick, Is.Zero);
            Assert.That(fixture.Manager.ProjectileCount, Is.Zero);
            Assert.That(afterStop.QueuedProjectileCount, Is.Zero);
            Assert.That(afterStop.ReservedProjectileCount, Is.Zero);
            Assert.That(afterStop.OccupiedSlotCount, Is.Zero);
            Assert.That(afterStop.AvailableSlotCount, Is.EqualTo(4));

            Assert.That(
                fixture.Manager.TryPeekInputProjectile(fixture.NexusId, 0, out _),
                Is.False);
        }

        private MovementFixture CreateRunningFixture(float targetX)
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
                catalog, "definitions",
                new List<TowerCombatDefinition> { generator, fire, water, wind, earth, nexus });

            TowerNetworkManager manager = new TowerNetworkManager(catalog);
            manager.BeginLevelSession(1);

            TowerNodeId generatorId = manager.RegisterTower(generator, Position(0f));
            TowerNodeId nexusId = manager.RegisterTower(nexus, Position(targetX));

            Assert.That(manager.TryRewire(generatorId, nexusId, out string linkError), Is.True, linkError);
            Assert.That(manager.TryStartSimulation(out string startError), Is.True, startError);

            return new MovementFixture(manager, generatorId, nexusId);
        }

        private static TowerProjectileSnapshot GetOnlyProjectile(TowerNetworkManager manager)
        {
            IReadOnlyList<TowerProjectileSnapshot> projectiles = manager.CreateProjectileSnapshot();

            Assert.That(projectiles.Count, Is.EqualTo(1));
            return projectiles[0];
        }

        private static TowerInputPortSnapshot GetInputSnapshot(
            TowerNetworkManager manager, TowerNodeId nodeId)
        {
            Assert.That(
                manager.TryCreateInputPortSnapshot(nodeId, 0, out TowerInputPortSnapshot snapshot),
                Is.True);

            return snapshot;
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

        private readonly struct MovementFixture
        {
            public MovementFixture(
                TowerNetworkManager manager, TowerNodeId generatorId, TowerNodeId nexusId)
            {
                Manager = manager;
                GeneratorId = generatorId;
                NexusId = nexusId;
            }

            public TowerNetworkManager Manager { get; }
            public TowerNodeId GeneratorId { get; }
            public TowerNodeId NexusId { get; }
        }
    }
}