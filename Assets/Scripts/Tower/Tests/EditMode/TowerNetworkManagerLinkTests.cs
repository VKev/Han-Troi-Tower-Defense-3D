using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace TowerDefense3D.Towers.Tests.EditMode
{
    public sealed class TowerNetworkManagerLinkTests
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
        public void GeneratorToElement_CreatesPortZeroLink()
        {
            Fixture fixture = CreateFixture();
            fixture.Manager.BeginLevelSession(1);

            TowerNodeId generator =
                fixture.Manager.RegisterTower(
                    fixture.Generator,
                    Position(0f));

            TowerNodeId fire =
                fixture.Manager.RegisterTower(
                    fixture.Fire,
                    Position(1f));

            Assert.That(
                fixture.Manager.TryRewire(
                    generator,
                    fire,
                    out string error),
                Is.True,
                error);

            Assert.That(
                fixture.Manager.LinkCount,
                Is.EqualTo(1));

            Assert.That(
                fixture.Manager.TryGetOutgoingLink(
                    generator,
                    out TowerLinkSnapshot link),
                Is.True);

            Assert.That(link.Source, Is.EqualTo(generator));
            Assert.That(link.Target, Is.EqualTo(fire));
            Assert.That(link.TargetInputPort, Is.Zero);
        }

        [Test]
        public void Rewire_ReplacesSourcesOldOutgoingLink()
        {
            Fixture fixture = CreateFixture();
            fixture.Manager.BeginLevelSession(1);

            TowerNodeId generator =
                fixture.Manager.RegisterTower(
                    fixture.Generator,
                    Position(0f));

            TowerNodeId fire =
                fixture.Manager.RegisterTower(
                    fixture.Fire,
                    Position(1f));

            TowerNodeId water =
                fixture.Manager.RegisterTower(
                    fixture.Water,
                    Position(2f));

            Assert.That(
                fixture.Manager.TryRewire(
                    generator,
                    fire,
                    out _),
                Is.True);

            Assert.That(
                fixture.Manager.TryRewire(
                    generator,
                    water,
                    out _),
                Is.True);

            Assert.That(fixture.Manager.LinkCount, Is.EqualTo(1));

            fixture.Manager.TryGetOutgoingLink(
                generator,
                out TowerLinkSnapshot link);

            Assert.That(link.Target, Is.EqualTo(water));
        }

        [Test]
        public void NormalTarget_NewSourceDisplacesOldIncomingSource()
        {
            Fixture fixture = CreateFixture();
            fixture.Manager.BeginLevelSession(1);

            TowerNodeId generatorA =
                fixture.Manager.RegisterTower(
                    fixture.Generator,
                    Position(0f));

            TowerNodeId generatorB =
                fixture.Manager.RegisterTower(
                    fixture.Generator,
                    Position(1f));

            TowerNodeId fire =
                fixture.Manager.RegisterTower(
                    fixture.Fire,
                    Position(2f));

            Assert.That(
                fixture.Manager.TryRewire(
                    generatorA,
                    fire,
                    out _),
                Is.True);

            Assert.That(
                fixture.Manager.TryRewire(
                    generatorB,
                    fire,
                    out _),
                Is.True);

            Assert.That(
                fixture.Manager.TryGetOutgoingLink(
                    generatorA,
                    out _),
                Is.False);

            Assert.That(
                fixture.Manager.TryGetOutgoingLink(
                    generatorB,
                    out TowerLinkSnapshot link),
                Is.True);

            Assert.That(link.Target, Is.EqualTo(fire));
        }

        [Test]
        public void Nexus_UsesTwoPortsAndRejectsThirdWithoutLosingOldLink()
        {
            Fixture fixture = CreateFixture();
            fixture.Manager.BeginLevelSession(1);

            TowerNodeId generatorA =
                fixture.Manager.RegisterTower(
                    fixture.Generator,
                    Position(0f));

            TowerNodeId generatorB =
                fixture.Manager.RegisterTower(
                    fixture.Generator,
                    Position(1f));

            TowerNodeId generatorC =
                fixture.Manager.RegisterTower(
                    fixture.Generator,
                    Position(2f));

            TowerNodeId water =
                fixture.Manager.RegisterTower(
                    fixture.Water,
                    Position(3f));

            TowerNodeId nexus =
                fixture.Manager.RegisterTower(
                    fixture.Nexus,
                    Position(4f));

            Assert.That(
                fixture.Manager.TryRewire(
                    generatorA,
                    nexus,
                    out _),
                Is.True);

            Assert.That(
                fixture.Manager.TryRewire(
                    generatorB,
                    nexus,
                    out _),
                Is.True);

            Assert.That(
                fixture.Manager.TryRewire(
                    generatorC,
                    water,
                    out _),
                Is.True);

            Assert.That(
                fixture.Manager.TryRewire(
                    generatorC,
                    nexus,
                    out string error),
                Is.False);

            StringAssert.Contains("occupied", error);

            Assert.That(
                fixture.Manager.TryGetOutgoingLink(
                    generatorC,
                    out TowerLinkSnapshot preserved),
                Is.True);

            Assert.That(
                preserved.Target,
                Is.EqualTo(water));

            fixture.Manager.TryGetOutgoingLink(
                generatorA,
                out TowerLinkSnapshot first);

            fixture.Manager.TryGetOutgoingLink(
                generatorB,
                out TowerLinkSnapshot second);

            Assert.That(first.TargetInputPort, Is.EqualTo(0));
            Assert.That(second.TargetInputPort, Is.EqualTo(1));
        }

        [Test]
        public void OutOfRangeRewire_PreservesOriginalLink()
        {
            Fixture fixture = CreateFixture();
            fixture.Manager.BeginLevelSession(1);

            TowerNodeId generator =
                fixture.Manager.RegisterTower(
                    fixture.Generator,
                    Position(0f));

            TowerNodeId fire =
                fixture.Manager.RegisterTower(
                    fixture.Fire,
                    Position(1f));

            TowerNodeId farWater =
                fixture.Manager.RegisterTower(
                    fixture.Water,
                    Position(13f));

            Assert.That(
                fixture.Manager.TryRewire(
                    generator,
                    fire,
                    out _),
                Is.True);

            Assert.That(
                fixture.Manager.TryRewire(
                    generator,
                    farWater,
                    out string error),
                Is.False);

            StringAssert.Contains("range", error);

            fixture.Manager.TryGetOutgoingLink(
                generator,
                out TowerLinkSnapshot preserved);

            Assert.That(preserved.Target, Is.EqualTo(fire));
        }

        [Test]
        public void CycleAttempt_IsRejectedAndOriginalGraphSurvives()
        {
            Fixture fixture = CreateFixture();
            fixture.Manager.BeginLevelSession(1);

            TowerNodeId generator =
                fixture.Manager.RegisterTower(
                    fixture.Generator,
                    Position(0f));

            TowerNodeId fire =
                fixture.Manager.RegisterTower(
                    fixture.Fire,
                    Position(1f));

            TowerNodeId water =
                fixture.Manager.RegisterTower(
                    fixture.Water,
                    Position(2f));

            Assert.That(
                fixture.Manager.TryRewire(
                    generator,
                    fire,
                    out _),
                Is.True);

            Assert.That(
                fixture.Manager.TryRewire(
                    fire,
                    water,
                    out _),
                Is.True);

            Assert.That(
                fixture.Manager.TryRewire(
                    water,
                    fire,
                    out string error),
                Is.False);

            StringAssert.Contains("cycle", error);

            Assert.That(fixture.Manager.LinkCount, Is.EqualTo(2));

            fixture.Manager.TryGetOutgoingLink(
                generator,
                out TowerLinkSnapshot first);

            fixture.Manager.TryGetOutgoingLink(
                fire,
                out TowerLinkSnapshot second);

            Assert.That(first.Target, Is.EqualTo(fire));
            Assert.That(second.Target, Is.EqualTo(water));
        }

        [Test]
        public void UnlinkAll_RemovesIncomingAndOutgoing()
        {
            Fixture fixture = CreateFixture();
            fixture.Manager.BeginLevelSession(1);

            TowerNodeId generator =
                fixture.Manager.RegisterTower(
                    fixture.Generator,
                    Position(0f));

            TowerNodeId fire =
                fixture.Manager.RegisterTower(
                    fixture.Fire,
                    Position(1f));

            TowerNodeId nexus =
                fixture.Manager.RegisterTower(
                    fixture.Nexus,
                    Position(2f));

            fixture.Manager.TryRewire(
                generator,
                fire,
                out _);

            fixture.Manager.TryRewire(
                fire,
                nexus,
                out _);

            Assert.That(
                fixture.Manager.TryUnlinkAll(
                    fire,
                    out string error),
                Is.True,
                error);

            Assert.That(fixture.Manager.LinkCount, Is.Zero);
        }

        [Test]
        public void UnregisterTower_RemovesDanglingLinks()
        {
            Fixture fixture = CreateFixture();
            fixture.Manager.BeginLevelSession(1);

            TowerNodeId generator =
                fixture.Manager.RegisterTower(
                    fixture.Generator,
                    Position(0f));

            TowerNodeId fire =
                fixture.Manager.RegisterTower(
                    fixture.Fire,
                    Position(1f));

            fixture.Manager.TryRewire(
                generator,
                fire,
                out _);

            Assert.That(
                fixture.Manager.UnregisterTower(fire),
                Is.True);

            Assert.That(fixture.Manager.LinkCount, Is.Zero);
        }

        [Test]
        public void LinkSnapshot_IsOrderedBySourceNodeId()
        {
            Fixture fixture = CreateFixture();
            fixture.Manager.BeginLevelSession(1);

            TowerNodeId generator =
                fixture.Manager.RegisterTower(
                    fixture.Generator,
                    Position(0f));

            TowerNodeId fire =
                fixture.Manager.RegisterTower(
                    fixture.Fire,
                    Position(1f));

            TowerNodeId water =
                fixture.Manager.RegisterTower(
                    fixture.Water,
                    Position(2f));

            TowerNodeId nexus =
                fixture.Manager.RegisterTower(
                    fixture.Nexus,
                    Position(3f));

            // Tạo link source ID lớn trước.
            fixture.Manager.TryRewire(
                water,
                nexus,
                out _);

            fixture.Manager.TryRewire(
                generator,
                fire,
                out _);

            IReadOnlyList<TowerLinkSnapshot> links =
                fixture.Manager.CreateLinkSnapshot();

            Assert.That(links.Count, Is.EqualTo(2));
            Assert.That(links[0].Source, Is.EqualTo(generator));
            Assert.That(links[1].Source, Is.EqualTo(water));
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
                fire,
                water,
                nexus);
        }

        private T Create<T>() where T : ScriptableObject
        {
            T value = ScriptableObject.CreateInstance<T>();
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

            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }

        private static TowerWorldPosition Position(float x)
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
                FireTowerDefinition fire,
                WaterTowerDefinition water,
                SoulNexusDefinition nexus)
            {
                Manager = manager;
                Generator = generator;
                Fire = fire;
                Water = water;
                Nexus = nexus;
            }

            public TowerNetworkManager Manager { get; }
            public GeneratorTowerDefinition Generator { get; }
            public FireTowerDefinition Fire { get; }
            public WaterTowerDefinition Water { get; }
            public SoulNexusDefinition Nexus { get; }
        }
    }
}