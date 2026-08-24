using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace TowerDefense3D.Towers.Tests.EditMode
{
    public sealed class TowerRuntimeSpecFactoryTests
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
        public void Generator_ConvertsToTwentyTickPhysicalSource()
        {
            GeneratorTowerDefinition definition =
                Create<GeneratorTowerDefinition>();

            TowerRuntimeSpec spec =
                TowerRuntimeSpecFactory.Create(
                    definition,
                    0.05f);

            Assert.That(
                spec.Family,
                Is.EqualTo(TowerFamily.Generator));
            Assert.That(
                spec.NetworkRole,
                Is.EqualTo(TowerNetworkRole.Source));
            Assert.That(spec.StableId, Is.EqualTo("generator"));
            Assert.That(spec.InputPortCount, Is.Zero);
            Assert.That(spec.OutputPortCount, Is.EqualTo(1));
            Assert.That(spec.QueueCapacityPerInput, Is.Zero);
            Assert.That(spec.CycleTicks, Is.EqualTo(20));
            Assert.That(spec.OutputProjectileCount, Is.EqualTo(1));
            Assert.That(
                spec.RequiredDownstreamReservationCount,
                Is.EqualTo(1));
            Assert.That(spec.SequenceSpacingTicks, Is.Zero);
            Assert.That(
                spec.OutputPayload.Kind,
                Is.EqualTo(ProjectilePayloadKind.Physical));
            Assert.That(spec.OutputPayload.Damage, Is.EqualTo(8f));
            Assert.That(
                spec.OutputPayload.DamageType,
                Is.EqualTo(DamageType.Physical));
        }

        [Test]
        public void Fire_ConvertsSecondsAndPreservesTotalCloneDamage()
        {
            FireTowerDefinition definition =
                Create<FireTowerDefinition>();

            TowerRuntimeSpec spec =
                TowerRuntimeSpecFactory.Create(
                    definition,
                    0.05f);

            Assert.That(spec.CycleTicks, Is.EqualTo(17));
            Assert.That(spec.OutputProjectileCount, Is.EqualTo(3));
            Assert.That(
                spec.RequiredDownstreamReservationCount,
                Is.EqualTo(3));
            Assert.That(spec.SequenceSpacingTicks, Is.EqualTo(2));
            Assert.That(
                spec.OutputPayload.Kind,
                Is.EqualTo(ProjectilePayloadKind.Fire));
            Assert.That(
                spec.OutputPayload.Damage,
                Is.EqualTo(6f).Within(0.0001f));
            Assert.That(
                spec.OutputPayload.DamageType,
                Is.EqualTo(DamageType.Magic));
        }

        [Test]
        public void WaterWindEarth_CreateReplacementPayloads()
        {
            TowerRuntimeSpec water =
                TowerRuntimeSpecFactory.Create(
                    Create<WaterTowerDefinition>(),
                    0.05f);

            TowerRuntimeSpec wind =
                TowerRuntimeSpecFactory.Create(
                    Create<WindTowerDefinition>(),
                    0.05f);

            TowerRuntimeSpec earth =
                TowerRuntimeSpecFactory.Create(
                    Create<EarthTowerDefinition>(),
                    0.05f);

            Assert.That(
                water.OutputPayload.Kind,
                Is.EqualTo(ProjectilePayloadKind.Water));
            Assert.That(water.OutputPayload.Damage, Is.EqualTo(5f));

            Assert.That(
                wind.OutputPayload.Kind,
                Is.EqualTo(ProjectilePayloadKind.Wind));
            Assert.That(wind.OutputPayload.Damage, Is.EqualTo(5f));

            Assert.That(
                earth.OutputPayload.Kind,
                Is.EqualTo(ProjectilePayloadKind.Earth));
            Assert.That(earth.OutputPayload.Damage, Is.EqualTo(6f));
            Assert.That(
                earth.OutputPayload.DamageType,
                Is.EqualTo(DamageType.Physical));
        }

        [Test]
        public void Nexus_ConvertsToTwoInputNonProducingSink()
        {
            SoulNexusDefinition definition =
                Create<SoulNexusDefinition>();

            TowerRuntimeSpec spec =
                TowerRuntimeSpecFactory.Create(
                    definition,
                    0.05f);

            Assert.That(
                spec.NetworkRole,
                Is.EqualTo(TowerNetworkRole.Sink));
            Assert.That(spec.InputPortCount, Is.EqualTo(2));
            Assert.That(spec.OutputPortCount, Is.Zero);
            Assert.That(spec.QueueCapacityPerInput, Is.EqualTo(4));
            Assert.That(spec.CycleTicks, Is.EqualTo(15));
            Assert.That(spec.OutputProjectileCount, Is.Zero);
            Assert.That(
                spec.RequiredDownstreamReservationCount,
                Is.Zero);
        }

        [TestCase(0f)]
        [TestCase(-0.05f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void Factory_RejectsInvalidTick(float tickSeconds)
        {
            GeneratorTowerDefinition definition =
                Create<GeneratorTowerDefinition>();

            Assert.Throws<ArgumentOutOfRangeException>(
                () => TowerRuntimeSpecFactory.Create(
                    definition,
                    tickSeconds));
        }

        [Test]
        public void RuntimeSpec_RejectsPartialBatchReservation()
        {
            var payload = new ProjectilePayload(
                ProjectilePayloadKind.Fire,
                6f,
                DamageType.Magic);

            Assert.Throws<ArgumentException>(
                () => new TowerRuntimeSpec(
                    TowerFamily.Fire,
                    TowerNetworkRole.Processor,
                    "fire",
                    1,
                    1,
                    3,
                    17,
                    3,
                    2,
                    2,
                    payload));
        }

        private T Create<T>() where T : ScriptableObject
        {
            T instance =
                ScriptableObject.CreateInstance<T>();
            owned.Add(instance);
            return instance;
        }
    }
}