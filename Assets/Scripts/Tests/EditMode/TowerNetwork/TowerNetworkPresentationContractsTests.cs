using System;
using NUnit.Framework;
using TowerDefense3D.GridPlacement;
using UnityEngine;

namespace TowerDefense3D.Towers.Tests.EditMode
{
    public sealed class TowerNetworkPresentationContractsTests
    {
        [Test]
        public void QueueSummary_StoresCombinedCapacityAndAvailableSlots()
        {
            var summary = new TowerQueueSummary(2, 1, 5);

            Assert.That(summary.QueuedProjectileCount, Is.EqualTo(2));
            Assert.That(summary.ReservedProjectileCount, Is.EqualTo(1));
            Assert.That(summary.Capacity, Is.EqualTo(5));
            Assert.That(summary.AvailableSlotCount, Is.EqualTo(2));
        }

        [Test]
        public void QueueSummary_RejectsNegativeOrOverCapacityValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TowerQueueSummary(-1, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TowerQueueSummary(0, -1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TowerQueueSummary(1, 1, 1));
        }

        [Test]
        public void RuntimeView_RequiresDefinitionBeforeNodeBindingAndCanClearSessionBinding()
        {
            var owner = new GameObject("Tower Runtime View Test");
            var definition = ScriptableObject.CreateInstance<GeneratorTowerDefinition>();

            try
            {
                TowerRuntimeView view = owner.AddComponent<TowerRuntimeView>();
                Assert.Throws<InvalidOperationException>(() => view.BindNode(new TowerNodeId(1)));

                view.Configure(definition);
                view.BindNode(new TowerNodeId(7));

                Assert.That(view.CombatDefinition, Is.SameAs(definition));
                Assert.That(view.NodeId, Is.EqualTo(new TowerNodeId(7)));
                Assert.That(view.IsRegistered, Is.True);

                view.ClearNodeBinding();
                Assert.That(view.IsRegistered, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void RuntimeView_UsesRendererCenterForProjectileOrigin()
        {
            var owner = new GameObject("Tower Runtime Anchor Test");
            var definition = ScriptableObject.CreateInstance<GeneratorTowerDefinition>();
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(owner.transform, false);
            visual.transform.localPosition = new Vector3(0f, 2f, 0f);
            visual.transform.localScale = new Vector3(2f, 4f, 2f);

            try
            {
                owner.transform.position = new Vector3(3f, 4f, 5f);
                TowerRuntimeView view = owner.AddComponent<TowerRuntimeView>();
                view.Configure(definition);
                Renderer renderer = visual.GetComponent<Renderer>();

                Assert.That(Vector3.Distance(view.ProjectileOrigin, renderer.bounds.center), Is.LessThan(0.001f));
                Assert.That(view.PresentationAnchor.y, Is.EqualTo(renderer.bounds.max.y + 0.2f).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void ProjectileView_ReplaysAuthoredVfxAndResetsForPool()
        {
            var owner = new GameObject("Tower Projectile View Test");
            ParticleSystem particles = owner.AddComponent<ParticleSystem>();

            try
            {
                TowerProjectileView view = owner.AddComponent<TowerProjectileView>();
                view.Initialize();
                var snapshot = new TowerProjectileSnapshot(
                    9,
                    new TowerNodeId(1),
                    new TowerNodeId(2),
                    new TowerWorldPosition(3f, 4f, 5f),
                    new ProjectilePayload(ProjectilePayloadKind.Water, 2f, DamageType.Magic),
                    0);

                view.Show(snapshot);

                Assert.That(view.ProjectileId, Is.EqualTo(9));
                Assert.That(view.transform.position, Is.EqualTo(new Vector3(3f, 4f, 5f)));
                Assert.That(particles.isPlaying, Is.True);
                Assert.That(view.GetComponent<Collider>(), Is.Null);

                view.ResetForPool();
                Assert.That(view.ProjectileId, Is.Zero);
                Assert.That(view.gameObject.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ProjectilePool_UsesRequestedPrefabAndReusesItsView()
        {
            var owner = new GameObject("Tower Projectile Pool Test");
            var projectilePrefab = new GameObject("Projectile Prefab");
            projectilePrefab.AddComponent<ParticleSystem>();

            try
            {
                TowerProjectilePoolView pool = owner.AddComponent<TowerProjectilePoolView>();
                pool.Initialize();
                pool.Show(1L, projectilePrefab, new Vector3(1f, 2f, 3f));

                TowerProjectileView first = owner.GetComponentInChildren<TowerProjectileView>(true);
                Assert.That(first, Is.Not.Null);
                Assert.That(first.ProjectileId, Is.EqualTo(1L));
                Assert.That(first.transform.position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
                Assert.That(pool.ActiveViewCount, Is.EqualTo(1));

                pool.Release(1L);
                Assert.That(pool.ActiveViewCount, Is.Zero);
                Assert.That(pool.InactiveViewCount, Is.EqualTo(1));

                pool.Show(2L, projectilePrefab, Vector3.one);
                TowerProjectileView second = owner.GetComponentInChildren<TowerProjectileView>(true);
                Assert.That(second, Is.SameAs(first));
                Assert.That(second.ProjectileId, Is.EqualTo(2L));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(projectilePrefab);
            }
        }

        [Test]
        public void ProjectilePool_PositionUpdateKeepsVfxPlayingAndFacesTravelDirection()
        {
            var owner = new GameObject("Tower Projectile Pool Position Test");
            var projectilePrefab = new GameObject("Projectile Prefab");
            projectilePrefab.AddComponent<ParticleSystem>();

            try
            {
                TowerProjectilePoolView pool = owner.AddComponent<TowerProjectilePoolView>();
                pool.Initialize();
                pool.Show(1L, projectilePrefab, Vector3.zero);

                TowerProjectileView view = owner.GetComponentInChildren<TowerProjectileView>(true);
                ParticleSystem particles = view.GetComponent<ParticleSystem>();
                particles.Simulate(0.25f, true, false);
                float elapsedTime = particles.time;
                var nextPosition = new Vector3(1f, 2f, 3f);
                Vector3 travelDirection = nextPosition.normalized;

                pool.Show(1L, projectilePrefab, nextPosition);

                Assert.That(view.transform.position, Is.EqualTo(nextPosition));
                Assert.That(Vector3.Dot(view.transform.forward, travelDirection), Is.GreaterThan(0.999f));
                Assert.That(elapsedTime, Is.GreaterThan(0f));
                Assert.That(particles.time, Is.EqualTo(elapsedTime).Within(0.001f));

                Quaternion travelRotation = view.transform.rotation;
                pool.Show(1L, projectilePrefab, nextPosition);
                Assert.That(Quaternion.Angle(view.transform.rotation, travelRotation), Is.LessThan(0.001f));

                pool.Release(1L);
                Assert.That(Quaternion.Angle(view.transform.localRotation, Quaternion.identity), Is.LessThan(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(projectilePrefab);
            }
        }

        [Test]
        public void ProjectilePool_WaitsForAuthoredTrailBeforeReusingView()
        {
            var owner = new GameObject("Tower Projectile Trail Pool Test");
            var projectilePrefab = new GameObject("Projectile Prefab");
            projectilePrefab.AddComponent<ParticleSystem>();
            TrailRenderer trail = projectilePrefab.AddComponent<TrailRenderer>();
            trail.time = 0.25f;

            try
            {
                TowerProjectilePoolView pool = owner.AddComponent<TowerProjectilePoolView>();
                pool.Initialize();
                pool.Show(1L, projectilePrefab, Vector3.zero);

                TowerProjectileView view = owner.GetComponentInChildren<TowerProjectileView>(true);
                pool.Show(1L, projectilePrefab, Vector3.one);
                pool.Release(1L);

                Assert.That(pool.ActiveViewCount, Is.Zero);
                Assert.That(pool.InactiveViewCount, Is.Zero);
                Assert.That(view.gameObject.activeSelf, Is.True);

                pool.AdvanceReleaseDelays(0.2f);
                Assert.That(view.gameObject.activeSelf, Is.True);

                pool.AdvanceReleaseDelays(0.06f);
                Assert.That(pool.InactiveViewCount, Is.EqualTo(1));
                Assert.That(view.gameObject.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(projectilePrefab);
            }
        }
    }
}
