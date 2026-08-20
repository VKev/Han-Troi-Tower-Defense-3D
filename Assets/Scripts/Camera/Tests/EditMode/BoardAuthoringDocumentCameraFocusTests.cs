using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TowerDefense3D.GridPlacement.Editor;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.GridPlacement.Tests.EditMode
{
    /// <summary>
    /// Unit-level coverage for <see cref="BoardAuthoringDocument.SetCameraFocus"/>:
    /// the independent CameraFocus bit toggle that ORs/ANDs only that bit while
    /// preserving any existing preset flags on the same cell, the same
    /// None-removal convention already used by <see cref="BoardAuthoringDocument.Paint"/>,
    /// and <see cref="BoardAuthoringDocument.Validate"/> no longer reporting
    /// CameraFocus as an unknown flag bit.
    /// </summary>
    public sealed class BoardAuthoringDocumentCameraFocusTests
    {
        private readonly List<BoardDefinition> transientDefinitions = new List<BoardDefinition>();

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
            foreach (BoardDefinition definition in transientDefinitions)
            {
                if (definition != null && !AssetDatabase.Contains(definition))
                {
                    UnityEngine.Object.DestroyImmediate(definition);
                }
            }

            transientDefinitions.Clear();
        }

        [Test]
        public void SetCameraFocus_EnableOnCellWithExistingPresetFlags_AddsCameraFocusAndPreservesExistingBits()
        {
            GridCell coordinate = new GridCell(1, 1, 0);
            BoardDefinition definition = CreateTransientBoard(
                new GridDimensions(3, 3, 2),
                new[]
                {
                    new BoardCellDefinition(
                        coordinate,
                        BoardCellFlags.SupportsPlacement | BoardCellFlags.Buildable)
                });
            var document = new BoardAuthoringDocument(definition);

            document.SetCameraFocus(coordinate, true);

            Assert.That(
                document.GetFlags(coordinate),
                Is.EqualTo(
                    BoardCellFlags.SupportsPlacement
                    | BoardCellFlags.Buildable
                    | BoardCellFlags.CameraFocus));
        }

        [Test]
        public void SetCameraFocus_DisableAfterEnable_RemovesOnlyCameraFocusAndPreservesOtherBits()
        {
            GridCell coordinate = new GridCell(1, 1, 0);
            BoardDefinition definition = CreateTransientBoard(
                new GridDimensions(3, 3, 2),
                new[]
                {
                    new BoardCellDefinition(
                        coordinate,
                        BoardCellFlags.SupportsPlacement | BoardCellFlags.Buildable)
                });
            var document = new BoardAuthoringDocument(definition);
            document.SetCameraFocus(coordinate, true);

            document.SetCameraFocus(coordinate, false);

            Assert.That(
                document.GetFlags(coordinate),
                Is.EqualTo(BoardCellFlags.SupportsPlacement | BoardCellFlags.Buildable));
        }

        [Test]
        public void SetCameraFocus_EnableOnCellWithNoOtherFlags_CreatesEntryWithOnlyCameraFocus()
        {
            GridCell coordinate = new GridCell(2, 0, 1);
            BoardDefinition definition = CreateTransientBoard(
                new GridDimensions(3, 3, 2),
                Array.Empty<BoardCellDefinition>());
            var document = new BoardAuthoringDocument(definition);
            Assert.That(document.ActiveCellCount, Is.Zero);

            document.SetCameraFocus(coordinate, true);

            Assert.That(document.GetFlags(coordinate), Is.EqualTo(BoardCellFlags.CameraFocus));
            Assert.That(document.ActiveCellCount, Is.EqualTo(1));
        }

        [Test]
        public void SetCameraFocus_DisableOnCellWithOnlyCameraFocus_RemovesCellEntryEntirely()
        {
            GridCell coordinate = new GridCell(2, 0, 1);
            BoardDefinition definition = CreateTransientBoard(
                new GridDimensions(3, 3, 2),
                Array.Empty<BoardCellDefinition>());
            var document = new BoardAuthoringDocument(definition);
            document.SetCameraFocus(coordinate, true);
            Assert.That(document.ActiveCellCount, Is.EqualTo(1));

            document.SetCameraFocus(coordinate, false);

            Assert.That(document.GetFlags(coordinate), Is.EqualTo(BoardCellFlags.None));
            Assert.That(document.ActiveCellCount, Is.Zero);
        }

        [Test]
        public void Validate_CellWithOnlyCameraFocusBit_ReportsNoUnknownFlagWarning()
        {
            BoardDefinition definition = CreateTransientBoard(
                new GridDimensions(3, 3, 2),
                new[]
                {
                    new BoardCellDefinition(new GridCell(1, 1, 0), BoardCellFlags.CameraFocus)
                });
            var document = new BoardAuthoringDocument(definition);

            var issues = document.Validate();

            foreach (string issue in issues)
            {
                StringAssert.DoesNotContain("unknown flag", issue.ToLowerInvariant());
            }

            Assert.That(issues, Is.Empty);
        }

        private BoardDefinition CreateTransientBoard(
            GridDimensions dimensions,
            BoardCellDefinition[] cells)
        {
            BoardDefinition definition = ScriptableObject.CreateInstance<BoardDefinition>();
            transientDefinitions.Add(definition);
            SetField(definition, "dimensions", dimensions);
            SetField(definition, "cellSize", 1f);
            SetField(definition, "heightUnit", 1f);
            SetField(definition, "cells", cells);
            return definition;
        }

        private static void SetField<T>(UnityEngine.Object target, string name, T value)
        {
            FieldInfo field = GetField(name);
            field.SetValue(target, value);
        }

        private static FieldInfo GetField(string name)
        {
            FieldInfo field = typeof(BoardDefinition).GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Expected serialized field '{name}'.");
            return field;
        }
    }
}
