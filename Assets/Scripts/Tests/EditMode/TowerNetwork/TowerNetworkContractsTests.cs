using System;
using NUnit.Framework;

namespace TowerDefense3D.Towers.Tests.EditMode
{
    public sealed class TowerNetworkContractsTests
    {
        [Test]
        public void NodeId_DefaultIsInvalid_AndPositiveValueIsValid()
        {
            TowerNodeId empty = default;
            TowerNodeId valid = new TowerNodeId(7);

            Assert.That(empty.IsValid, Is.False);
            Assert.That(valid.IsValid, Is.True);
            Assert.That(valid.Value, Is.EqualTo(7));
        }

        [Test]
        public void NodeId_EqualityUsesOnlyStoredValue()
        {
            TowerNodeId first = new TowerNodeId(3);
            TowerNodeId same = new TowerNodeId(3);
            TowerNodeId different = new TowerNodeId(4);

            Assert.That(first.Equals(same), Is.True);
            Assert.That(first.Equals(different), Is.False);
            Assert.That(
                first.GetHashCode(),
                Is.EqualTo(same.GetHashCode()));
        }

        [Test]
        public void Distance_UsesThreeDimensionalPythagoras()
        {
            var first =
                new TowerWorldPosition(0f, 0f, 0f);
            var second =
                new TowerWorldPosition(3f, 4f, 0f);

            float distance =
                TowerWorldPosition.Distance(first, second);

            Assert.That(distance, Is.EqualTo(5f).Within(0.0001f));
        }

        [Test]
        public void MoveTowards_MovesAtMostRequestedDistance()
        {
            var current =
                new TowerWorldPosition(0f, 0f, 0f);
            var target =
                new TowerWorldPosition(3f, 4f, 0f);

            TowerWorldPosition result =
                TowerWorldPosition.MoveTowards(
                    current,
                    target,
                    2.5f);

            Assert.That(result.X, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(result.Y, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(result.Z, Is.Zero.Within(0.0001f));
        }

        [Test]
        public void MoveTowards_WhenStepReachesTarget_ReturnsExactTarget()
        {
            var current =
                new TowerWorldPosition(0f, 0f, 0f);
            var target =
                new TowerWorldPosition(0.2f, 0f, 0f);

            TowerWorldPosition result =
                TowerWorldPosition.MoveTowards(
                    current,
                    target,
                    0.5f);

            Assert.That(result.X, Is.EqualTo(target.X));
            Assert.That(result.Y, Is.EqualTo(target.Y));
            Assert.That(result.Z, Is.EqualTo(target.Z));
        }

        [Test]
        public void MoveTowards_RejectsNegativeMovement()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => TowerWorldPosition.MoveTowards(
                    new TowerWorldPosition(),
                    new TowerWorldPosition(1f, 0f, 0f),
                    -1f));
        }

        [Test]
        public void ProjectilePayload_StoresKindDamageAndDamageType()
        {
            var payload = new ProjectilePayload(
                ProjectilePayloadKind.Fire,
                6f,
                DamageType.Magic);

            Assert.That(
                payload.Kind,
                Is.EqualTo(ProjectilePayloadKind.Fire));
            Assert.That(payload.Damage, Is.EqualTo(6f));
            Assert.That(
                payload.DamageType,
                Is.EqualTo(DamageType.Magic));
        }

        [Test]
        public void ElementPayload_IsANewReplacementValue()
        {
            var generatorPayload = new ProjectilePayload(
                ProjectilePayloadKind.Physical,
                8f,
                DamageType.Physical);

            var firePayload = new ProjectilePayload(
                ProjectilePayloadKind.Fire,
                6f,
                DamageType.Magic);

            Assert.That(
                generatorPayload.Kind,
                Is.EqualTo(ProjectilePayloadKind.Physical));
            Assert.That(generatorPayload.Damage, Is.EqualTo(8f));

            Assert.That(
                firePayload.Kind,
                Is.EqualTo(ProjectilePayloadKind.Fire));
            Assert.That(firePayload.Damage, Is.EqualTo(6f));
            Assert.That(
                firePayload.Damage,
                Is.Not.EqualTo(
                    generatorPayload.Damage +
                    firePayload.Damage));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void ProjectilePayload_RejectsInvalidDamage(float damage)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ProjectilePayload(
                    ProjectilePayloadKind.Water,
                    damage,
                    DamageType.Magic));
        }
        [Test]
        public void LinkSnapshot_KeepsSourceTargetAndNexusPort()
        {
            TowerNodeId source = new TowerNodeId(3);
            TowerNodeId target = new TowerNodeId(8);

            var snapshot = new TowerLinkSnapshot(
                source,
                target,
                1);

            Assert.That(snapshot.Source, Is.EqualTo(source));
            Assert.That(snapshot.Target, Is.EqualTo(target));
            Assert.That(snapshot.TargetInputPort, Is.EqualTo(1));
        }
        [Test]
        public void LinkSnapshot_RejectsInvalidIdentityOrPort()
        {
            TowerNodeId valid = new TowerNodeId(1);

            Assert.Throws<ArgumentException>(
                () => new TowerLinkSnapshot(
                    default,
                    valid,
                    0));

            Assert.Throws<ArgumentException>(
                () => new TowerLinkSnapshot(
                    valid,
                    default,
                    0));

            Assert.Throws<ArgumentOutOfRangeException>(
                () => new TowerLinkSnapshot(
                    valid,
                    valid,
                    -1));
        }

        [Test]
        public void ProjectileSnapshot_CopiesPresentationData()
        {
            TowerNodeId source = new TowerNodeId(2);
            TowerNodeId target = new TowerNodeId(5);
            var position =
                new TowerWorldPosition(1f, 2f, 3f);
            var payload = new ProjectilePayload(
                ProjectilePayloadKind.Fire,
                2f,
                DamageType.Magic);

            var snapshot = new TowerProjectileSnapshot(
                17,
                source,
                target,
                position,
                payload,
                4);

            Assert.That(snapshot.ProjectileId, Is.EqualTo(17));
            Assert.That(snapshot.Source, Is.EqualTo(source));
            Assert.That(snapshot.Target, Is.EqualTo(target));
            Assert.That(snapshot.Position.X, Is.EqualTo(1f));
            Assert.That(snapshot.Position.Y, Is.EqualTo(2f));
            Assert.That(snapshot.Position.Z, Is.EqualTo(3f));
            Assert.That(
                snapshot.Payload.Kind,
                Is.EqualTo(ProjectilePayloadKind.Fire));
            Assert.That(snapshot.Payload.Damage, Is.EqualTo(2f));
            Assert.That(snapshot.LaunchDelayTicks, Is.EqualTo(4));
        }

        [Test]
        public void ProjectileSnapshot_RejectsInvalidIdentityAndDelay()
        {
            TowerNodeId source = new TowerNodeId(1);
            TowerNodeId target = new TowerNodeId(2);
            var payload = new ProjectilePayload(
                ProjectilePayloadKind.Physical,
                8f,
                DamageType.Physical);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => new TowerProjectileSnapshot(
                    0,
                    source,
                    target,
                    default,
                    payload,
                    0));

            Assert.Throws<ArgumentException>(
                () => new TowerProjectileSnapshot(
                    1,
                    default,
                    target,
                    default,
                    payload,
                    0));

            Assert.Throws<ArgumentException>(
                () => new TowerProjectileSnapshot(
                    1,
                    source,
                    default,
                    default,
                    payload,
                    0));

            Assert.Throws<ArgumentOutOfRangeException>(
                () => new TowerProjectileSnapshot(
                    1,
                    source,
                    target,
                    default,
                    payload,
                    -1));
        }
    }
}