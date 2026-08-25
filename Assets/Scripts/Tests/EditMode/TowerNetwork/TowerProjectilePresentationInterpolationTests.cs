using System;
using NUnit.Framework;
using UnityEngine;

namespace TowerDefense3D.Towers.Tests.EditMode
{
    public sealed class TowerProjectilePresentationInterpolationTests
    {
        private GameObject projectilePrefab;

        [SetUp]
        public void SetUp()
        {
            projectilePrefab = new GameObject("Projectile Prefab");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(projectilePrefab);
        }

        [Test]
        public void Track_InterpolatesOnlyBetweenAdjacentSimulationTicks()
        {
            TowerProjectilePresentationTrack track =
                TowerProjectilePresentationTrack.Create(Snapshot(0f), projectilePrefab);

            track.Advance(Snapshot(1f));

            Assert.That(track.CalculateRenderedPosition(0f), Is.EqualTo(new Vector3(0f, 0f, 0f)));
            Assert.That(track.CalculateRenderedPosition(0.5f), Is.EqualTo(new Vector3(0.5f, 0f, 0f)));
            Assert.That(track.CalculateRenderedPosition(1f), Is.EqualTo(new Vector3(1f, 0f, 0f)));
        }

        [Test]
        public void Track_CatchUpKeepsOnlyTheFinalAdjacentPair()
        {
            TowerProjectilePresentationTrack track =
                TowerProjectilePresentationTrack.Create(Snapshot(0f), projectilePrefab);

            track.Advance(Snapshot(1f));
            track.Advance(Snapshot(2f));
            track.Advance(Snapshot(3f));

            Assert.That(track.CalculateRenderedPosition(0.5f), Is.EqualTo(new Vector3(2.5f, 0f, 0f)));
        }

        [Test]
        public void Track_DelayedProjectileStaysHiddenUntilLaunchStarts()
        {
            TowerProjectilePresentationTrack track =
                TowerProjectilePresentationTrack.Create(Snapshot(0f, 1), projectilePrefab);

            Assert.That(track.IsVisible, Is.False);

            track.Advance(Snapshot(0f, 0));

            Assert.That(track.IsVisible, Is.True);
            Assert.That(track.CalculateRenderedPosition(0.5f), Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Track_RetirementRendersTargetBeforeRelease()
        {
            TowerProjectilePresentationTrack track =
                TowerProjectilePresentationTrack.Create(Snapshot(0.5f), projectilePrefab);

            track.BeginRetirement(new TowerWorldPosition(1f, 0f, 0f));

            Assert.That(track.IsRetiring, Is.True);
            Assert.That(track.ReleaseAfterRender, Is.False);
            Assert.That(track.CalculateRenderedPosition(0.5f), Is.EqualTo(new Vector3(0.75f, 0f, 0f)));

            track.PrepareReleaseAfterRender();

            Assert.That(track.ReleaseAfterRender, Is.True);
            Assert.That(track.CalculateRenderedPosition(0f), Is.EqualTo(new Vector3(1f, 0f, 0f)));
        }

        [Test]
        public void Track_RejectsAChangedProjectileIdentity()
        {
            TowerProjectilePresentationTrack track =
                TowerProjectilePresentationTrack.Create(Snapshot(0f), projectilePrefab);
            TowerProjectileSnapshot anotherProjectile = Snapshot(1f, 0, 2L);

            Assert.Throws<ArgumentException>(() => track.Advance(anotherProjectile));
        }

        [Test]
        public void Track_PreservesSourceProjectilePrefab()
        {
            TowerProjectilePresentationTrack track =
                TowerProjectilePresentationTrack.Create(Snapshot(0f), projectilePrefab);

            Assert.That(track.Source, Is.EqualTo(new TowerNodeId(1)));
            Assert.That(track.ProjectilePrefab, Is.SameAs(projectilePrefab));
        }

        private static TowerProjectileSnapshot Snapshot(float x, int launchDelayTicks = 0, long projectileId = 1L)
        {
            return new TowerProjectileSnapshot(
                projectileId,
                new TowerNodeId(1),
                new TowerNodeId(2),
                new TowerWorldPosition(x, 0f, 0f),
                new ProjectilePayload(ProjectilePayloadKind.Physical, 1f, DamageType.Physical),
                launchDelayTicks);
        }
    }
}
