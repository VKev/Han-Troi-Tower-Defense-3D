using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace TowerDefense3D.Towers.Tests.EditMode
{
    public sealed class TowerNetworkManagerGenerationTests
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
        public void Generator_WaitsFullCycleBeforeFirstEmission()
        {
            GenerationFixture fixture = CreateRunningFixture();

            StepTicks(fixture.Manager, 19);

            TowerNodeSimulationSnapshot beforeEmission = GetNodeSnapshot(fixture.Manager, fixture.GeneratorId);

            Assert.That(fixture.Manager.CurrentTick, Is.EqualTo(19));
            Assert.That(fixture.Manager.ProjectileCount, Is.Zero);
            Assert.That(beforeEmission.CycleTicks, Is.EqualTo(20));
            Assert.That(beforeEmission.CycleProgressTicks, Is.EqualTo(19));
            Assert.That(beforeEmission.RemainingCycleTicks, Is.EqualTo(1));
            Assert.That(beforeEmission.IsReady, Is.False);

            Assert.That(fixture.Manager.StepOneTick(), Is.True);

            TowerNodeSimulationSnapshot afterEmission = GetNodeSnapshot(fixture.Manager, fixture.GeneratorId);

            Assert.That(fixture.Manager.CurrentTick, Is.EqualTo(20));
            Assert.That(fixture.Manager.ProjectileCount, Is.EqualTo(1));
            Assert.That(afterEmission.CycleProgressTicks, Is.Zero);
            Assert.That(afterEmission.RemainingCycleTicks, Is.EqualTo(20));
            Assert.That(afterEmission.IsReady, Is.False);
        }

        [Test]
        public void Generator_ReservesTargetAndCreatesPhysicalProjectile()
        {
            GenerationFixture fixture = CreateRunningFixture();

            StepTicks(fixture.Manager, 20);

            IReadOnlyList<TowerProjectileSnapshot> projectiles = fixture.Manager.CreateProjectileSnapshot();

            Assert.That(projectiles.Count, Is.EqualTo(1));

            TowerProjectileSnapshot projectile = projectiles[0];

            Assert.That(projectile.ProjectileId, Is.EqualTo(1L));
            Assert.That(projectile.Source, Is.EqualTo(fixture.GeneratorId));
            Assert.That(projectile.Target, Is.EqualTo(fixture.NexusId));
            Assert.That(projectile.Position.X, Is.EqualTo(0f));
            Assert.That(projectile.Position.Y, Is.EqualTo(0f));
            Assert.That(projectile.Position.Z, Is.EqualTo(0f));
            Assert.That(projectile.Payload.Kind, Is.EqualTo(ProjectilePayloadKind.Physical));
            Assert.That(projectile.Payload.DamageType, Is.EqualTo(DamageType.Physical));
            Assert.That(projectile.Payload.Damage, Is.GreaterThan(0f));
            Assert.That(projectile.LaunchDelayTicks, Is.Zero);

            TowerInputPortSnapshot input = GetInputSnapshot(fixture.Manager, fixture.NexusId, inputPort: 0);

            Assert.That(input.Capacity, Is.EqualTo(4));
            Assert.That(input.QueuedProjectileCount, Is.Zero);
            Assert.That(input.ReservedProjectileCount, Is.EqualTo(1));
            Assert.That(input.OccupiedSlotCount, Is.EqualTo(1));
            Assert.That(input.AvailableSlotCount, Is.EqualTo(3));
        }

        [Test]
        public void ProjectileSpawnPlan_IsCompleteBeforeRuntimePlayback()
        {
            GenerationFixture fixture = CreateRunningFixture();

            IReadOnlyList<TowerProjectileSpawnOrder> plan =
                fixture.Manager.EnsureProjectileSpawnPlanThrough(60L);

            Assert.That(fixture.Manager.CurrentTick, Is.Zero);
            Assert.That(fixture.Manager.ProjectileCount, Is.Zero);
            Assert.That(plan.Count, Is.EqualTo(3));
            Assert.That(plan[0].SpawnTick, Is.EqualTo(20L));
            Assert.That(plan[1].SpawnTick, Is.EqualTo(40L));
            Assert.That(plan[2].SpawnTick, Is.EqualTo(60L));
            Assert.That(plan[0].Projectile.ProjectileId, Is.EqualTo(1L));
            Assert.That(plan[1].Projectile.ProjectileId, Is.EqualTo(2L));
            Assert.That(plan[2].Projectile.ProjectileId, Is.EqualTo(3L));

            StepTicks(fixture.Manager, 60);

            IReadOnlyList<TowerProjectileSnapshot> activeProjectiles =
                fixture.Manager.CreateProjectileSnapshot();
            Assert.That(activeProjectiles.Count, Is.EqualTo(1));
            Assert.That(activeProjectiles[0].ProjectileId, Is.EqualTo(3L));
            Assert.That(activeProjectiles[0].Position.X, Is.EqualTo(0f));
        }

        [Test]
        public void ProjectileSpawnPlan_ExtendsWithoutChangingRuntimePlayback()
        {
            GenerationFixture fixture = CreateRunningFixture();

            IReadOnlyList<TowerProjectileSpawnOrder> initialPlan =
                fixture.Manager.EnsureProjectileSpawnPlanThrough(40L);

            StepTicks(fixture.Manager, 25);

            IReadOnlyList<TowerProjectileSpawnOrder> extendedPlan =
                fixture.Manager.EnsureProjectileSpawnPlanThrough(60L);

            Assert.That(initialPlan.Count, Is.EqualTo(2));
            Assert.That(extendedPlan.Count, Is.EqualTo(3));
            Assert.That(extendedPlan[0].SpawnTick, Is.EqualTo(20L));
            Assert.That(extendedPlan[0].Projectile.ProjectileId, Is.EqualTo(1L));
            Assert.That(extendedPlan[1].SpawnTick, Is.EqualTo(40L));
            Assert.That(extendedPlan[1].Projectile.ProjectileId, Is.EqualTo(2L));
            Assert.That(extendedPlan[2].SpawnTick, Is.EqualTo(60L));
            Assert.That(extendedPlan[2].Projectile.ProjectileId, Is.EqualTo(3L));

            StepTicks(fixture.Manager, 35);

            IReadOnlyList<TowerProjectileSnapshot> activeProjectiles =
                fixture.Manager.CreateProjectileSnapshot();
            Assert.That(activeProjectiles.Count, Is.EqualTo(1));
            Assert.That(activeProjectiles[0].ProjectileId, Is.EqualTo(3L));
        }

        [Test]
        public void NexusConsumption_ReleasesCapacityForLaterGeneratorProjectiles()
        {
            GenerationFixture fixture = CreateRunningFixture();

            StepTicks(fixture.Manager, 22);

            TowerInputPortSnapshot firstArrival =
                GetInputSnapshot(fixture.Manager, fixture.NexusId, inputPort: 0);

            Assert.That(firstArrival.QueuedProjectileCount, Is.EqualTo(1));
            Assert.That(firstArrival.ReservedProjectileCount, Is.Zero);

            Assert.That(
                fixture.Manager.TryPeekInputProjectile(
                    fixture.NexusId,
                    0,
                    out ProjectileQueueEntry firstProjectile),
                Is.True);

            Assert.That(firstProjectile.ProjectileId, Is.EqualTo(1L));
            Assert.That(firstProjectile.ArrivalTick, Is.EqualTo(22L));

            StepTicks(fixture.Manager, 13);

            TowerNodeSimulationSnapshot nexusBeforeConsume =
                GetNodeSnapshot(fixture.Manager, fixture.NexusId);

            Assert.That(fixture.Manager.CurrentTick, Is.EqualTo(35));
            Assert.That(nexusBeforeConsume.CycleProgressTicks, Is.EqualTo(14));

            Assert.That(fixture.Manager.StepOneTick(), Is.True);

            TowerInputPortSnapshot afterFirstConsume =
                GetInputSnapshot(fixture.Manager, fixture.NexusId, inputPort: 0);

            Assert.That(fixture.Manager.CurrentTick, Is.EqualTo(36));
            Assert.That(afterFirstConsume.QueuedProjectileCount, Is.Zero);
            Assert.That(afterFirstConsume.ReservedProjectileCount, Is.Zero);

            Assert.That(
                fixture.Manager.TryPeekInputProjectile(
                    fixture.NexusId,
                    0,
                    out _),
                Is.False);

            StepTicks(fixture.Manager, 6);

            TowerInputPortSnapshot secondArrival =
                GetInputSnapshot(fixture.Manager, fixture.NexusId, inputPort: 0);

            Assert.That(fixture.Manager.CurrentTick, Is.EqualTo(42));
            Assert.That(secondArrival.QueuedProjectileCount, Is.EqualTo(1));
            Assert.That(secondArrival.ReservedProjectileCount, Is.Zero);

            Assert.That(
                fixture.Manager.TryPeekInputProjectile(
                    fixture.NexusId,
                    0,
                    out ProjectileQueueEntry secondProjectile),
                Is.True);

            Assert.That(secondProjectile.ProjectileId, Is.EqualTo(2L));
            Assert.That(secondProjectile.ArrivalTick, Is.EqualTo(42L));
        }

        [Test]
        public void DisconnectedGenerator_DoesNotAdvanceWhileAnotherChainRuns()
        {
            GenerationFixture fixture = CreateRunningFixture(includeDisconnectedGenerator: true);

            StepTicks(fixture.Manager, 20);

            TowerNodeSimulationSnapshot connectedGenerator =
                GetNodeSnapshot(fixture.Manager, fixture.GeneratorId);

            TowerNodeSimulationSnapshot disconnectedGenerator =
                GetNodeSnapshot(fixture.Manager, fixture.DisconnectedGeneratorId);

            Assert.That(connectedGenerator.BelongsToValidChain, Is.True);
            Assert.That(connectedGenerator.CycleProgressTicks, Is.Zero);

            Assert.That(disconnectedGenerator.BelongsToValidChain, Is.False);
            Assert.That(disconnectedGenerator.CycleProgressTicks, Is.Zero);
            Assert.That(disconnectedGenerator.RemainingCycleTicks, Is.EqualTo(20));
            Assert.That(disconnectedGenerator.IsReady, Is.False);

            Assert.That(fixture.Manager.ProjectileCount, Is.EqualTo(1));

            TowerProjectileSnapshot projectile = fixture.Manager.CreateProjectileSnapshot()[0];

            Assert.That(projectile.Source, Is.EqualTo(fixture.GeneratorId));
            Assert.That(projectile.Source, Is.Not.EqualTo(fixture.DisconnectedGeneratorId));
        }

        [Test]
        public void StopSimulation_ClearsProjectilesReservationsAndCycleProgress()
        {
            GenerationFixture fixture = CreateRunningFixture();

            StepTicks(fixture.Manager, 20);

            Assert.That(fixture.Manager.IsRunning, Is.True);
            Assert.That(fixture.Manager.CurrentTick, Is.EqualTo(20));
            Assert.That(fixture.Manager.ProjectileCount, Is.EqualTo(1));

            TowerInputPortSnapshot beforeStop = GetInputSnapshot(fixture.Manager, fixture.NexusId, inputPort: 0);

            Assert.That(beforeStop.ReservedProjectileCount, Is.EqualTo(1));

            fixture.Manager.StopSimulation();

            Assert.That(fixture.Manager.IsRunning, Is.False);
            Assert.That(fixture.Manager.CurrentTick, Is.Zero);
            Assert.That(fixture.Manager.ProjectileCount, Is.Zero);
            Assert.That(fixture.Manager.CreateProjectileSnapshot(), Is.Empty);

            TowerNodeSimulationSnapshot generator = GetNodeSnapshot(fixture.Manager, fixture.GeneratorId);

            Assert.That(generator.CycleProgressTicks, Is.Zero);
            Assert.That(generator.RemainingCycleTicks, Is.EqualTo(20));
            Assert.That(generator.IsReady, Is.False);

            TowerInputPortSnapshot afterStop = GetInputSnapshot(fixture.Manager, fixture.NexusId, inputPort: 0);

            Assert.That(afterStop.QueuedProjectileCount, Is.Zero);
            Assert.That(afterStop.ReservedProjectileCount, Is.Zero);
            Assert.That(afterStop.OccupiedSlotCount, Is.Zero);
            Assert.That(afterStop.AvailableSlotCount, Is.EqualTo(4));
            Assert.That(fixture.Manager.StepOneTick(), Is.False);
        }

        private GenerationFixture CreateRunningFixture(bool includeDisconnectedGenerator = false)
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
            TowerNodeId nexusId = manager.RegisterTower(nexus, Position(1f));
            TowerNodeId disconnectedGeneratorId = default;

            if (includeDisconnectedGenerator)
            {
                disconnectedGeneratorId = manager.RegisterTower(generator, Position(2f));
            }

            Assert.That(manager.TryRewire(generatorId, nexusId, out string linkError), Is.True, linkError);
            Assert.That(manager.TryStartSimulation(out string startError), Is.True, startError);

            return new GenerationFixture(manager, generatorId, nexusId, disconnectedGeneratorId);
        }

        private static TowerNodeSimulationSnapshot GetNodeSnapshot(
            TowerNetworkManager manager, TowerNodeId nodeId)
        {
            Assert.That(
                manager.TryCreateNodeSimulationSnapshot(nodeId, out TowerNodeSimulationSnapshot snapshot),
                Is.True);

            return snapshot;
        }

        private static TowerInputPortSnapshot GetInputSnapshot(
            TowerNetworkManager manager, TowerNodeId nodeId, int inputPort)
        {
            Assert.That(
                manager.TryCreateInputPortSnapshot(nodeId, inputPort, out TowerInputPortSnapshot snapshot),
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

        private readonly struct GenerationFixture
        {
            public GenerationFixture(
                TowerNetworkManager manager, TowerNodeId generatorId, TowerNodeId nexusId,
                TowerNodeId disconnectedGeneratorId)
            {
                Manager = manager;
                GeneratorId = generatorId;
                NexusId = nexusId;
                DisconnectedGeneratorId = disconnectedGeneratorId;
            }

            public TowerNetworkManager Manager { get; }
            public TowerNodeId GeneratorId { get; }
            public TowerNodeId NexusId { get; }
            public TowerNodeId DisconnectedGeneratorId { get; }
        }
    }
}
