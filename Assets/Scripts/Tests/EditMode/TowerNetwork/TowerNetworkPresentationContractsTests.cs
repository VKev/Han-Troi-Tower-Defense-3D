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
        public void ProjectileView_UsesOnlyRendererPresentationAndResetsForPool()
        {
            var owner = new GameObject("Tower Projectile View Test");
            Shader shader = Shader.Find("Sprites/Default");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);

            try
            {
                TowerProjectileView view = owner.AddComponent<TowerProjectileView>();
                view.Initialize(material);
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
                Assert.That(view.GetComponent<LineRenderer>(), Is.Not.Null);
                Assert.That(view.GetComponent<Collider>(), Is.Null);

                view.ResetForPool();
                Assert.That(view.ProjectileId, Is.Zero);
                Assert.That(view.gameObject.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void PlacementRecord_PreservesCombatAndPlacementIdentity()
        {
            var owner = new GameObject("Tower Placement Record Test");
            var combatDefinition = ScriptableObject.CreateInstance<GeneratorTowerDefinition>();
            var placementDefinition = ScriptableObject.CreateInstance<TowerDefinition>();

            try
            {
                TowerRuntimeView view = owner.AddComponent<TowerRuntimeView>();
                view.Configure(combatDefinition);
                var record = new TowerPlacementRecord(
                    combatDefinition,
                    placementDefinition,
                    view,
                    new GridCell(2, 3, 1),
                    11);

                Assert.That(record.CombatDefinition, Is.SameAs(combatDefinition));
                Assert.That(record.PlacementDefinition, Is.SameAs(placementDefinition));
                Assert.That(record.RuntimeView, Is.SameAs(view));
                Assert.That(record.Anchor, Is.EqualTo(new GridCell(2, 3, 1)));
                Assert.That(record.OwnerId, Is.EqualTo(11));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(combatDefinition);
                UnityEngine.Object.DestroyImmediate(placementDefinition);
            }
        }
    }
}
