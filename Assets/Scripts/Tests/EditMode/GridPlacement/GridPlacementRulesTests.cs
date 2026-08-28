using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TowerDefense3D.Towers;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.GridPlacement.Tests.EditMode
{
    public sealed class GridPlacementRulesTests
    {
        private readonly List<BoardDefinition> definitions = new List<BoardDefinition>();

        [TearDown]
        public void TearDown()
        {
            foreach (BoardDefinition definition in definitions)
            {
                Object.DestroyImmediate(definition);
            }

            definitions.Clear();
        }

        [Test]
        public void Mapper_UsesXZPlaneAndRoundedVerticalLevels()
        {
            BoardDefinition definition = CreateBoard(
                new GridDimensions(4, 5, 4),
                2f,
                1.5f,
                new BoardCellDefinition[0]);
            var board = new GridBoard(definition, new Vector3(10f, 0f, 20f));

            bool mapped = board.Mapper.TryWorldToCell(
                new Vector3(13.9f, 3.1f, 24.2f),
                out GridCell cell);

            Assert.That(mapped, Is.True);
            Assert.That(cell, Is.EqualTo(new GridCell(1, 2, 2)));
            Assert.That(
                board.Mapper.CellToWorldCenter(cell),
                Is.EqualTo(new Vector3(13f, 3f, 25f)));
            Assert.That(
                board.Mapper.TryWorldToCell(new Vector3(9.99f, 0f, 20f), out _),
                Is.False);
        }

        [Test]
        public void EvenFootprint_RemainderExtendsTowardPositiveXAndZ()
        {
            var anchor = new GridCell(4, 7, 2);
            var footprint = new TowerFootprint(2, 4, 3);
            var cells = new GridCell[FootprintEnumerator.RequiredBaseCellCount(footprint)];

            bool written = FootprintEnumerator.TryWriteBaseCells(
                anchor,
                footprint,
                cells,
                out int count);

            Assert.That(written, Is.True);
            Assert.That(count, Is.EqualTo(8));
            Assert.That(cells[0], Is.EqualTo(new GridCell(4, 6, 2)));
            Assert.That(cells[count - 1], Is.EqualTo(new GridCell(5, 9, 2)));
        }

        [Test]
        public void Validator_ChecksEveryBaseSupportAndFullBlockedVolume()
        {
            BoardCellFlags buildable =
                BoardCellFlags.SupportsPlacement | BoardCellFlags.Buildable;
            var authored = new List<BoardCellDefinition>();
            for (int z = 0; z < 4; z++)
            {
                for (int x = 0; x < 4; x++)
                {
                    authored.Add(new BoardCellDefinition(new GridCell(x, z, 0), buildable));
                }
            }

            authored.Add(new BoardCellDefinition(
                new GridCell(2, 2, 1),
                BoardCellFlags.StaticBlocker));
            BoardDefinition definition = CreateBoard(
                new GridDimensions(4, 4, 4),
                1f,
                1f,
                authored.ToArray());
            var board = new GridBoard(definition, Vector3.zero);
            var occupancy = new GridOccupancy(definition.Dimensions);
            var validator = new PlacementValidator(board, occupancy);

            PlacementResult result = validator.Evaluate(
                new GridCell(1, 1, 0),
                new TowerFootprint(2, 2, 2));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(
                result.Failures & PlacementFailureFlags.StaticBlocker,
                Is.EqualTo(PlacementFailureFlags.StaticBlocker));
        }

        [Test]
        public void Validator_RejectsMissingSupportWithoutMutatingOccupancy()
        {
            BoardCellFlags buildable =
                BoardCellFlags.SupportsPlacement | BoardCellFlags.Buildable;
            BoardDefinition definition = CreateBoard(
                new GridDimensions(3, 3, 3),
                1f,
                1f,
                new[]
                {
                    new BoardCellDefinition(new GridCell(1, 1, 0), buildable)
                });
            var board = new GridBoard(definition, Vector3.zero);
            var occupancy = new GridOccupancy(definition.Dimensions);
            var validator = new PlacementValidator(board, occupancy);

            PlacementResult result = validator.Evaluate(
                new GridCell(1, 1, 0),
                new TowerFootprint(2, 2, 1));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(
                result.Failures & PlacementFailureFlags.MissingSupport,
                Is.EqualTo(PlacementFailureFlags.MissingSupport));
            Assert.That(occupancy.IsOccupied(new GridCell(1, 1, 0)), Is.False);
        }

        [TestCase(BoardCellFlags.Road)]
        [TestCase(BoardCellFlags.RoadSpawn)]
        [TestCase(BoardCellFlags.RoadEnd)]
        public void Validator_TreatsRoadRoleCellsAsNotBuildableEvenWhenBuildableIsSet(
            BoardCellFlags roadRole)
        {
            BoardCellFlags buildableRoad =
                BoardCellFlags.SupportsPlacement | BoardCellFlags.Buildable | roadRole;
            BoardDefinition definition = CreateBoard(
                new GridDimensions(3, 3, 3),
                1f,
                1f,
                new[]
                {
                    new BoardCellDefinition(new GridCell(1, 1, 0), buildableRoad)
                });
            var board = new GridBoard(definition, Vector3.zero);
            var occupancy = new GridOccupancy(definition.Dimensions);
            var validator = new PlacementValidator(board, occupancy);

            PlacementResult result = validator.Evaluate(
                new GridCell(1, 1, 0),
                new TowerFootprint(1, 1, 1));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(
                result.Failures & PlacementFailureFlags.NotBuildable,
                Is.EqualTo(PlacementFailureFlags.NotBuildable));
        }

        [Test]
        public void Validator_AllowsPlacementOnBuildableCellWithNoRoadRole()
        {
            BoardCellFlags buildable =
                BoardCellFlags.SupportsPlacement | BoardCellFlags.Buildable;
            BoardDefinition definition = CreateBoard(
                new GridDimensions(3, 3, 3),
                1f,
                1f,
                new[]
                {
                    new BoardCellDefinition(new GridCell(1, 1, 0), buildable)
                });
            var board = new GridBoard(definition, Vector3.zero);
            var occupancy = new GridOccupancy(definition.Dimensions);
            var validator = new PlacementValidator(board, occupancy);

            PlacementResult result = validator.Evaluate(
                new GridCell(1, 1, 0),
                new TowerFootprint(1, 1, 1));

            Assert.That(result.Succeeded, Is.True);
        }

        [Test]
        public void Reservation_IsAllOrNothingAndDisposeRollsBack()
        {
            var occupancy = new GridOccupancy(new GridDimensions(4, 4, 4));
            var footprint = new TowerFootprint(2, 2, 2);
            var anchor = new GridCell(1, 1, 0);

            Assert.That(
                occupancy.TryReserve(anchor, footprint, out PlacementReservation first),
                Is.True);
            Assert.That(
                occupancy.TryReserve(anchor, footprint, out PlacementReservation overlap),
                Is.False);
            Assert.That(overlap, Is.Null);

            first.Dispose();
            Assert.That(occupancy.IsOccupied(anchor), Is.False);
            Assert.That(
                occupancy.TryReserve(anchor, footprint, out PlacementReservation second),
                Is.True);
            Assert.That(second.Commit(42), Is.True);
            Assert.That(occupancy.TryGetOwner(anchor, out int owner), Is.True);
            Assert.That(owner, Is.EqualTo(42));

            occupancy.ReleaseOwner(42);
            Assert.That(occupancy.IsOccupied(anchor), Is.False);
        }

        [TestCase(
            "Assets/Config/GridPlacement/GeneratorTower.asset",
            "Assets/Config/Towers/Definitions/Sources/Generator.asset")]
        [TestCase(
            "Assets/Config/GridPlacement/FireTower.asset",
            "Assets/Config/Towers/Definitions/Elements/Fire.asset")]
        [TestCase(
            "Assets/Config/GridPlacement/WaterTower.asset",
            "Assets/Config/Towers/Definitions/Elements/Water.asset")]
        [TestCase(
            "Assets/Config/GridPlacement/WindTower.asset",
            "Assets/Config/Towers/Definitions/Elements/Wind.asset")]
        [TestCase(
            "Assets/Config/GridPlacement/SoulNexusTower.asset",
            "Assets/Config/Towers/Definitions/Sinks/SoulNexus.asset")]
        public void TowerInstanceFactory_GroundsAuthoredTowerAndCentersProjectileOrigin(
            string placementDefinitionPath,
            string combatDefinitionPath)
        {
            var factoryOwner = new GameObject("Tower Instance Factory Test");
            var placedRoot = new GameObject("Placed Towers Test");
            GameObject instance = null;

            try
            {
                TowerDefinition placementDefinition = AssetDatabase.LoadAssetAtPath<TowerDefinition>(
                    placementDefinitionPath);
                TowerCombatDefinition combatDefinition = AssetDatabase.LoadAssetAtPath<TowerCombatDefinition>(
                    combatDefinitionPath);
                Assert.That(placementDefinition, Is.Not.Null);
                Assert.That(combatDefinition, Is.Not.Null);

                TowerInstanceFactory factory = factoryOwner.AddComponent<TowerInstanceFactory>();
                SetField(factory, "placedObjectsRoot", placedRoot.transform);
                var placementPosition = new Vector3(3f, 4f, 5f);

                Assert.That(factory.TryCreate(placementDefinition, placementPosition, out instance), Is.True);

                Bounds bounds = CalculateCombinedBounds(instance);
                TowerRuntimeView runtimeView = instance.GetComponent<TowerRuntimeView>();
                Assert.That(runtimeView, Is.Not.Null);
                runtimeView.Configure(combatDefinition);

                Assert.That(bounds.min.y, Is.EqualTo(placementPosition.y).Within(0.001f));
                Assert.That(Vector3.Distance(runtimeView.ProjectileOrigin, bounds.center), Is.LessThan(0.001f));
                Assert.That(instance.transform.position.x, Is.EqualTo(placementPosition.x).Within(0.001f));
                Assert.That(instance.transform.position.z, Is.EqualTo(placementPosition.z).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(instance);
                Object.DestroyImmediate(factoryOwner);
                Object.DestroyImmediate(placedRoot);
            }
        }

        private static Bounds CalculateCombinedBounds(GameObject instance)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Is.Not.Empty);
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                if (renderers[index].enabled)
                {
                    bounds.Encapsulate(renderers[index].bounds);
                }
            }

            return bounds;
        }

        private BoardDefinition CreateBoard(
            GridDimensions dimensions,
            float cellSize,
            float heightUnit,
            BoardCellDefinition[] cells)
        {
            BoardDefinition definition = ScriptableObject.CreateInstance<BoardDefinition>();
            definitions.Add(definition);
            SetField(definition, "dimensions", dimensions);
            SetField(definition, "cellSize", cellSize);
            SetField(definition, "heightUnit", heightUnit);
            SetField(definition, "cells", cells);
            return definition;
        }

        private static void SetField<T>(Object target, string name, T value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Expected serialized field '{name}'.");
            field.SetValue(target, value);
        }
    }
}
