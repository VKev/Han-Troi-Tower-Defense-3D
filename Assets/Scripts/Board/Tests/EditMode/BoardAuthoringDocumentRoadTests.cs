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
    /// Unit-level coverage for <see cref="BoardAuthoringDocument.SetRoadRole"/>:
    /// the independent Road/RoadSpawn/RoadEnd bit group that behaves as a
    /// second, mutually-exclusive "preset-like" group, orthogonal to the
    /// existing preset and CameraFocus bits, plus the corresponding
    /// <see cref="BoardAuthoringDocument.Validate"/> coverage (known-flag
    /// masking and the three new soft warnings).
    /// </summary>
    public sealed class BoardAuthoringDocumentRoadTests
    {
        private const string TemporaryFolder =
            "Assets/Scripts/Board/Tests/EditMode/__BoardRoadTemp";

        private readonly List<BoardDefinition> transientDefinitions = new List<BoardDefinition>();

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
            if (AssetDatabase.IsValidFolder(TemporaryFolder))
            {
                AssetDatabase.DeleteAsset(TemporaryFolder);
            }

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
        public void SetRoadRole_SwitchingModesOnSameCell_KeepsExactlyOneRoadRoleBitSet()
        {
            GridCell coordinate = new GridCell(1, 1, 0);
            BoardDefinition definition = CreateTransientBoard(
                new GridDimensions(3, 3, 2),
                Array.Empty<BoardCellDefinition>());
            var document = new BoardAuthoringDocument(definition);

            document.SetRoadRole(coordinate, RoadPaintMode.Road);
            Assert.That(document.GetFlags(coordinate), Is.EqualTo(BoardCellFlags.Road));

            document.SetRoadRole(coordinate, RoadPaintMode.Spawn);
            Assert.That(document.GetFlags(coordinate), Is.EqualTo(BoardCellFlags.RoadSpawn));

            document.SetRoadRole(coordinate, RoadPaintMode.End);
            Assert.That(document.GetFlags(coordinate), Is.EqualTo(BoardCellFlags.RoadEnd));
        }

        [Test]
        public void SetRoadRole_PreservesUnrelatedPresetAndCameraFocusBits()
        {
            GridCell coordinate = new GridCell(0, 0, 0);
            BoardDefinition definition = CreateTransientBoard(
                new GridDimensions(3, 3, 2),
                new[]
                {
                    new BoardCellDefinition(
                        coordinate,
                        BoardCellFlags.SupportsPlacement
                        | BoardCellFlags.Buildable
                        | BoardCellFlags.CameraFocus)
                });
            var document = new BoardAuthoringDocument(definition);

            document.SetRoadRole(coordinate, RoadPaintMode.Spawn);

            Assert.That(
                document.GetFlags(coordinate),
                Is.EqualTo(
                    BoardCellFlags.SupportsPlacement
                    | BoardCellFlags.Buildable
                    | BoardCellFlags.CameraFocus
                    | BoardCellFlags.RoadSpawn));
        }

        [Test]
        public void SetRoadRole_None_RemovesCellEntryWhenNoOtherBitsRemain()
        {
            GridCell coordinate = new GridCell(2, 0, 1);
            BoardDefinition definition = CreateTransientBoard(
                new GridDimensions(3, 3, 2),
                Array.Empty<BoardCellDefinition>());
            var document = new BoardAuthoringDocument(definition);
            document.SetRoadRole(coordinate, RoadPaintMode.End);
            Assert.That(document.ActiveCellCount, Is.EqualTo(1));

            document.SetRoadRole(coordinate, RoadPaintMode.None);

            Assert.That(document.GetFlags(coordinate), Is.EqualTo(BoardCellFlags.None));
            Assert.That(document.ActiveCellCount, Is.Zero);
        }

        [Test]
        public void SetRoadRole_None_PreservesOtherBitsWithoutRemovingCellEntry()
        {
            GridCell coordinate = new GridCell(1, 1, 0);
            BoardDefinition definition = CreateTransientBoard(
                new GridDimensions(3, 3, 2),
                new[]
                {
                    new BoardCellDefinition(coordinate, BoardCellFlags.Buildable)
                });
            var document = new BoardAuthoringDocument(definition);
            document.SetRoadRole(coordinate, RoadPaintMode.Road);

            document.SetRoadRole(coordinate, RoadPaintMode.None);

            Assert.That(document.GetFlags(coordinate), Is.EqualTo(BoardCellFlags.Buildable));
            Assert.That(document.ActiveCellCount, Is.EqualTo(1));
        }

        [Test]
        public void Validate_RoadRoleCombinedWithKnownFlags_ReportsNoUnknownFlagWarning()
        {
            BoardDefinition definition = CreateTransientBoard(
                new GridDimensions(3, 3, 2),
                new[]
                {
                    new BoardCellDefinition(new GridCell(0, 0, 0), BoardCellFlags.Road),
                    new BoardCellDefinition(new GridCell(1, 0, 0), BoardCellFlags.RoadSpawn),
                    new BoardCellDefinition(new GridCell(2, 0, 0), BoardCellFlags.RoadEnd),
                    new BoardCellDefinition(
                        new GridCell(0, 1, 0),
                        BoardCellFlags.Road
                        | BoardCellFlags.CameraFocus
                        | BoardCellFlags.SupportsPlacement)
                });
            var document = new BoardAuthoringDocument(definition);

            var issues = document.Validate();

            foreach (string issue in issues)
            {
                StringAssert.DoesNotContain("unknown flag", issue.ToLowerInvariant());
            }
        }

        [Test]
        public void Validate_RoadCellsWithoutSpawnOrEnd_ReportsBothSoftWarnings()
        {
            BoardDefinition definition = CreateTransientBoard(
                new GridDimensions(3, 3, 2),
                new[]
                {
                    new BoardCellDefinition(new GridCell(0, 0, 0), BoardCellFlags.Road)
                });
            var document = new BoardAuthoringDocument(definition);

            var issues = document.Validate();

            Assert.That(issues, Has.Some.Contains("no Road Spawn cell"));
            Assert.That(issues, Has.Some.Contains("no Road End cell"));
        }

        [Test]
        public void Validate_RoadCellsWithSpawnAndEnd_ReportsNeitherSoftWarning()
        {
            BoardDefinition definition = CreateTransientBoard(
                new GridDimensions(3, 3, 2),
                new[]
                {
                    new BoardCellDefinition(new GridCell(0, 0, 0), BoardCellFlags.Road),
                    new BoardCellDefinition(new GridCell(1, 0, 0), BoardCellFlags.RoadSpawn),
                    new BoardCellDefinition(new GridCell(2, 0, 0), BoardCellFlags.RoadEnd)
                });
            var document = new BoardAuthoringDocument(definition);

            var issues = document.Validate();

            Assert.That(issues, Has.None.Contains("Road Spawn"));
            Assert.That(issues, Has.None.Contains("Road End"));
        }

        [Test]
        public void Validate_BoardWithNoRoadCellsAtAll_ReportsNoRoadSoftWarnings()
        {
            BoardDefinition definition = CreateTransientBoard(
                new GridDimensions(3, 3, 2),
                Array.Empty<BoardCellDefinition>());
            var document = new BoardAuthoringDocument(definition);

            var issues = document.Validate();

            Assert.That(issues, Is.Empty);
        }

        [Test]
        public void Validate_RoadAndBuildableOverlap_ReportsNoOverlapWarning()
        {
            // PlacementValidator now unconditionally rejects any Road/RoadSpawn/RoadEnd cell as
            // not buildable regardless of the Buildable bit, so this combination is no longer
            // flagged as a data-hygiene concern in the Board Painter.
            BoardDefinition definition = CreateTransientBoard(
                new GridDimensions(3, 3, 2),
                new[]
                {
                    new BoardCellDefinition(
                        new GridCell(0, 0, 0),
                        BoardCellFlags.Road | BoardCellFlags.Buildable),
                    new BoardCellDefinition(new GridCell(1, 0, 0), BoardCellFlags.RoadSpawn),
                    new BoardCellDefinition(new GridCell(2, 0, 0), BoardCellFlags.RoadEnd)
                });
            var document = new BoardAuthoringDocument(definition);

            var issues = document.Validate();

            Assert.That(issues, Has.None.Contains("Buildable"));
        }

        [Test]
        public void Validate_RoadWithoutBuildableOverlap_ReportsNoOverlapWarning()
        {
            BoardDefinition definition = CreateTransientBoard(
                new GridDimensions(3, 3, 2),
                new[]
                {
                    new BoardCellDefinition(new GridCell(0, 0, 0), BoardCellFlags.Road),
                    new BoardCellDefinition(new GridCell(1, 0, 0), BoardCellFlags.RoadSpawn),
                    new BoardCellDefinition(new GridCell(2, 0, 0), BoardCellFlags.RoadEnd)
                });
            var document = new BoardAuthoringDocument(definition);

            var issues = document.Validate();

            Assert.That(issues, Has.None.Contains("Buildable"));
        }

        [Test]
        public void Commit_UndoRedoRoundTripsAllThreeRoadRoleBits()
        {
            BoardDefinition definition = CreateTemporaryAsset(
                new GridDimensions(3, 3, 2),
                Array.Empty<BoardCellDefinition>());
            var document = new BoardAuthoringDocument(definition);
            GridCell roadCell = new GridCell(0, 0, 0);
            GridCell spawnCell = new GridCell(1, 0, 0);
            GridCell endCell = new GridCell(2, 0, 0);

            document.SetRoadRole(roadCell, RoadPaintMode.Road);
            document.SetRoadRole(spawnCell, RoadPaintMode.Spawn);
            document.SetRoadRole(endCell, RoadPaintMode.End);
            document.Commit("Paint Road Cells");

            Assert.That(document.GetFlags(roadCell), Is.EqualTo(BoardCellFlags.Road));
            Assert.That(document.GetFlags(spawnCell), Is.EqualTo(BoardCellFlags.RoadSpawn));
            Assert.That(document.GetFlags(endCell), Is.EqualTo(BoardCellFlags.RoadEnd));

            Undo.PerformUndo();
            document.Reload();
            Assert.That(document.GetFlags(roadCell), Is.EqualTo(BoardCellFlags.None));
            Assert.That(document.GetFlags(spawnCell), Is.EqualTo(BoardCellFlags.None));
            Assert.That(document.GetFlags(endCell), Is.EqualTo(BoardCellFlags.None));

            Undo.PerformRedo();
            document.Reload();
            Assert.That(document.GetFlags(roadCell), Is.EqualTo(BoardCellFlags.Road));
            Assert.That(document.GetFlags(spawnCell), Is.EqualTo(BoardCellFlags.RoadSpawn));
            Assert.That(document.GetFlags(endCell), Is.EqualTo(BoardCellFlags.RoadEnd));
        }

        [Test]
        public void TemporaryAsset_SaveAndReloadPreservesRoadRoleBits()
        {
            BoardDefinition definition = CreateTemporaryAsset(
                new GridDimensions(3, 3, 2),
                Array.Empty<BoardCellDefinition>());
            string path = AssetDatabase.GetAssetPath(definition);
            var document = new BoardAuthoringDocument(definition);
            GridCell coordinate = new GridCell(1, 1, 0);
            document.SetRoadRole(coordinate, RoadPaintMode.Spawn);
            document.Commit("Save road cell");
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            BoardDefinition reloaded = AssetDatabase.LoadAssetAtPath<BoardDefinition>(path);
            var reloadedDocument = new BoardAuthoringDocument(reloaded);

            Assert.That(reloadedDocument.GetFlags(coordinate), Is.EqualTo(BoardCellFlags.RoadSpawn));
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

        private BoardDefinition CreateTemporaryAsset(
            GridDimensions dimensions,
            BoardCellDefinition[] cells)
        {
            EnsureTemporaryFolder();
            BoardDefinition definition = ScriptableObject.CreateInstance<BoardDefinition>();
            SetField(definition, "dimensions", dimensions);
            SetField(definition, "cellSize", 1f);
            SetField(definition, "heightUnit", 1f);
            SetField(definition, "cells", cells);
            string path = TemporaryFolder + "/Board.asset";
            AssetDatabase.CreateAsset(definition, path);
            AssetDatabase.SaveAssets();
            return definition;
        }

        private static void EnsureTemporaryFolder()
        {
            const string parent = "Assets/Scripts/Board/Tests/EditMode";
            if (!AssetDatabase.IsValidFolder(TemporaryFolder))
            {
                AssetDatabase.CreateFolder(parent, "__BoardRoadTemp");
            }
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
