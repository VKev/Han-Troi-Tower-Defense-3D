using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace TowerDefense3D.GridPlacement.Tests.EditMode
{
    /// <summary>
    /// Unit-level coverage for <see cref="BoardCameraFocusRegionCalculator"/>:
    /// the no-focus-cells fallback, focus-region union correctness, and
    /// exclusion of <see cref="BoardCellFlags.CameraFocus"/> cells authored
    /// outside the board's lowest playable level.
    /// </summary>
    public sealed class BoardCameraFocusRegionCalculatorTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = created.Count - 1; index >= 0; index--)
            {
                if (created[index] != null)
                {
                    Object.DestroyImmediate(created[index]);
                }
            }

            created.Clear();
        }

        [Test]
        public void TryCalculate_NoCameraFocusCellsAnywhere_ReturnsFalse()
        {
            // Mirrors BoardCameraFramingTests.Bounds_UsesLowestSupportAndSameLevelBlockers,
            // which contains zero CameraFocus-flagged cells, to prove the calculator
            // reports "no focus region" and callers fall back to unchanged pre-feature
            // full lowest-level bounds.
            BoardDefinition board = CreateBoard(
                new GridDimensions(10, 10, 4),
                new[]
                {
                    Cell(5, 5, 2, BoardCellFlags.SupportsPlacement),
                    Cell(2, 3, 1, BoardCellFlags.SupportsPlacement),
                    Cell(7, 8, 1, BoardCellFlags.StaticBlocker),
                    Cell(0, 0, 0, BoardCellFlags.StaticBlocker),
                    Cell(20, 2, 1, BoardCellFlags.SupportsPlacement),
                });

            bool hasLowestLevel = LowestBoardLevelBoundsCalculator.TryCalculate(
                board,
                out LowestBoardLevelBounds lowestLevelBounds);
            bool hasFocusRegion = BoardCameraFocusRegionCalculator.TryCalculate(
                board,
                lowestLevelBounds,
                out LowestBoardLevelBounds focusBounds);

            Assert.That(hasLowestLevel, Is.True);
            Assert.That(
                lowestLevelBounds,
                Is.EqualTo(new LowestBoardLevelBounds(1, 2, 3, 8, 9)));
            Assert.That(hasFocusRegion, Is.False);
            Assert.That(focusBounds, Is.EqualTo(default(LowestBoardLevelBounds)));
        }

        [Test]
        public void TryCalculate_UnionsCameraFocusFlaggedCellsAtLowestLevel()
        {
            BoardDefinition board = CreateBoard(
                new GridDimensions(10, 10, 3),
                new[]
                {
                    Cell(1, 1, 0, BoardCellFlags.SupportsPlacement),
                    Cell(8, 8, 0, BoardCellFlags.SupportsPlacement),
                    Cell(2, 2, 0, BoardCellFlags.CameraFocus),
                    Cell(5, 6, 0, BoardCellFlags.CameraFocus),
                    Cell(3, 9, 1, BoardCellFlags.SupportsPlacement),
                });

            bool hasLowestLevel = LowestBoardLevelBoundsCalculator.TryCalculate(
                board,
                out LowestBoardLevelBounds lowestLevelBounds);
            bool hasFocusRegion = BoardCameraFocusRegionCalculator.TryCalculate(
                board,
                lowestLevelBounds,
                out LowestBoardLevelBounds focusBounds);

            Assert.That(hasLowestLevel, Is.True);
            Assert.That(
                lowestLevelBounds,
                Is.EqualTo(new LowestBoardLevelBounds(0, 1, 1, 9, 9)));
            Assert.That(hasFocusRegion, Is.True);
            Assert.That(
                focusBounds,
                Is.EqualTo(new LowestBoardLevelBounds(0, 2, 2, 6, 7)));
        }

        [Test]
        public void TryCalculate_CameraFocusOnlyOutsideLowestLevel_ReturnsFalse()
        {
            BoardDefinition board = CreateBoard(
                new GridDimensions(10, 10, 2),
                new[]
                {
                    Cell(0, 0, 0, BoardCellFlags.SupportsPlacement),
                    Cell(9, 9, 0, BoardCellFlags.SupportsPlacement),
                    Cell(4, 4, 1, BoardCellFlags.CameraFocus),
                });

            bool hasLowestLevel = LowestBoardLevelBoundsCalculator.TryCalculate(
                board,
                out LowestBoardLevelBounds lowestLevelBounds);
            bool hasFocusRegion = BoardCameraFocusRegionCalculator.TryCalculate(
                board,
                lowestLevelBounds,
                out LowestBoardLevelBounds focusBounds);

            Assert.That(hasLowestLevel, Is.True);
            Assert.That(lowestLevelBounds.Level, Is.EqualTo(0));
            Assert.That(hasFocusRegion, Is.False);
            Assert.That(focusBounds, Is.EqualTo(default(LowestBoardLevelBounds)));
        }

        [Test]
        public void TryCalculate_IgnoresCameraFocusCellAtHigherLevelWhenLowestLevelAlsoHasFocusCells()
        {
            BoardDefinition board = CreateBoard(
                new GridDimensions(10, 10, 2),
                new[]
                {
                    Cell(0, 0, 0, BoardCellFlags.SupportsPlacement),
                    Cell(9, 9, 0, BoardCellFlags.SupportsPlacement),
                    Cell(3, 3, 0, BoardCellFlags.CameraFocus),
                    Cell(7, 7, 1, BoardCellFlags.CameraFocus),
                });

            bool hasLowestLevel = LowestBoardLevelBoundsCalculator.TryCalculate(
                board,
                out LowestBoardLevelBounds lowestLevelBounds);
            bool hasFocusRegion = BoardCameraFocusRegionCalculator.TryCalculate(
                board,
                lowestLevelBounds,
                out LowestBoardLevelBounds focusBounds);

            Assert.That(hasLowestLevel, Is.True);
            Assert.That(hasFocusRegion, Is.True);
            // If the higher-level (7,7,1) cell leaked into the union, the bounds
            // would extend to include X=7/Z=7 instead of collapsing to the single
            // lowest-level focus cell at (3,3).
            Assert.That(
                focusBounds,
                Is.EqualTo(new LowestBoardLevelBounds(0, 3, 3, 4, 4)));
        }

        private BoardDefinition CreateBoard(
            GridDimensions dimensions,
            BoardCellDefinition[] cells)
        {
            BoardDefinition board = Track(ScriptableObject.CreateInstance<BoardDefinition>());
            SetField(board, "dimensions", dimensions);
            SetField(board, "cellSize", 1f);
            SetField(board, "heightUnit", 1f);
            SetField(board, "cells", cells);
            return board;
        }

        private T Track<T>(T value) where T : Object
        {
            created.Add(value);
            return value;
        }

        private static BoardCellDefinition Cell(
            int x,
            int z,
            int y,
            BoardCellFlags flags) =>
            new BoardCellDefinition(new GridCell(x, z, y), flags);

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
