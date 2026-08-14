using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TowerDefense3D.GridPlacement.Editor;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.GridPlacement.Tests.EditMode
{
    public sealed class BoardAuthoringTests
    {
        private const string TemporaryFolder =
            "Assets/_Project/GridPlacement/Tests/EditMode/__BoardPainterTemp";

        private readonly List<BoardDefinition> transientDefinitions =
            new List<BoardDefinition>();

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
        public void Presets_MapToApprovedFlags()
        {
            Assert.That(
                BoardPaintPresetUtility.GetFlags(BoardPaintPreset.Empty),
                Is.EqualTo(BoardCellFlags.None));
            Assert.That(
                BoardPaintPresetUtility.GetFlags(BoardPaintPreset.Buildable),
                Is.EqualTo(BoardCellFlags.SupportsPlacement | BoardCellFlags.Buildable));
            Assert.That(
                BoardPaintPresetUtility.GetFlags(BoardPaintPreset.NoBuild),
                Is.EqualTo(BoardCellFlags.SupportsPlacement));
            Assert.That(
                BoardPaintPresetUtility.GetFlags(BoardPaintPreset.BlockedSurface),
                Is.EqualTo(
                    BoardCellFlags.SupportsPlacement
                    | BoardCellFlags.Buildable
                    | BoardCellFlags.StaticBlocker));
            Assert.That(
                BoardPaintPresetUtility.GetFlags(BoardPaintPreset.VolumeBlocker),
                Is.EqualTo(BoardCellFlags.StaticBlocker));
        }

        [Test]
        public void Reload_MergesDuplicateFlagsWithoutDirtyingAsset()
        {
            GridCell coordinate = new GridCell(1, 2, 0);
            BoardDefinition definition = CreateTransientBoard(
                new GridDimensions(4, 4, 2),
                new[]
                {
                    new BoardCellDefinition(coordinate, BoardCellFlags.SupportsPlacement),
                    new BoardCellDefinition(coordinate, BoardCellFlags.Buildable)
                });
            EditorUtility.ClearDirty(definition);
            Assert.That(EditorUtility.IsDirty(definition), Is.False);

            var document = new BoardAuthoringDocument(definition);

            Assert.That(document.DuplicateCoordinateCount, Is.EqualTo(1));
            Assert.That(
                document.GetFlags(coordinate),
                Is.EqualTo(BoardCellFlags.SupportsPlacement | BoardCellFlags.Buildable));
            Assert.That(EditorUtility.IsDirty(definition), Is.False);
        }

        [Test]
        public void PaintEraseAndCommit_AffectOnlySelectedLevelAndSortYZX()
        {
            BoardDefinition definition = CreateTransientBoard(
                new GridDimensions(4, 4, 3),
                Array.Empty<BoardCellDefinition>());
            var document = new BoardAuthoringDocument(definition);

            document.Paint(new GridCell(3, 1, 2), BoardPaintPreset.Buildable);
            document.Paint(new GridCell(2, 2, 0), BoardPaintPreset.NoBuild);
            document.Paint(new GridCell(1, 2, 0), BoardPaintPreset.VolumeBlocker);
            document.Paint(new GridCell(0, 0, 1), BoardPaintPreset.BlockedSurface);
            document.Paint(new GridCell(3, 1, 2), BoardPaintPreset.Empty);
            document.Commit("Test board painting");

            BoardCellDefinition[] cells = GetCells(definition);
            Assert.That(cells, Has.Length.EqualTo(3));
            Assert.That(cells[0].Coordinate, Is.EqualTo(new GridCell(1, 2, 0)));
            Assert.That(cells[1].Coordinate, Is.EqualTo(new GridCell(2, 2, 0)));
            Assert.That(cells[2].Coordinate, Is.EqualTo(new GridCell(0, 0, 1)));
            Assert.That(
                document.GetFlags(new GridCell(3, 1, 2)),
                Is.EqualTo(BoardCellFlags.None));
        }

        [Test]
        public void PaintBrush_UsesCenteredOddSizesAndClipsAtBoardEdges()
        {
            var dimensions = new GridDimensions(7, 7, 3);
            BoardDefinition definition = CreateTransientBoard(
                dimensions,
                Array.Empty<BoardCellDefinition>());
            var document = new BoardAuthoringDocument(definition);

            Assert.That(
                BoardPainterWindow.PaintBrush(
                    document,
                    new GridCell(3, 3, 0),
                    1,
                    BoardPaintPreset.Buildable),
                Is.True);
            Assert.That(
                BoardPainterWindow.PaintBrush(
                    document,
                    new GridCell(3, 3, 1),
                    3,
                    BoardPaintPreset.NoBuild),
                Is.True);
            Assert.That(
                BoardPainterWindow.PaintBrush(
                    document,
                    new GridCell(0, 0, 2),
                    5,
                    BoardPaintPreset.VolumeBlocker),
                Is.True);

            Assert.That(CountActiveCells(document, dimensions, 0), Is.EqualTo(1));
            Assert.That(CountActiveCells(document, dimensions, 1), Is.EqualTo(9));
            Assert.That(CountActiveCells(document, dimensions, 2), Is.EqualTo(9));

            Assert.That(
                BoardPainterWindow.PaintBrush(
                    document,
                    new GridCell(0, 0, 2),
                    3,
                    BoardPaintPreset.Empty),
                Is.True);
            Assert.That(CountActiveCells(document, dimensions, 2), Is.EqualTo(5));
        }

        [Test]
        public void Resize_PreservesIntersectionAndCountsRemovedCells()
        {
            BoardDefinition definition = CreateTransientBoard(
                new GridDimensions(5, 5, 4),
                new[]
                {
                    new BoardCellDefinition(
                        new GridCell(1, 1, 1),
                        BoardCellFlags.SupportsPlacement),
                    new BoardCellDefinition(
                        new GridCell(4, 4, 3),
                        BoardCellFlags.StaticBlocker)
                });
            var document = new BoardAuthoringDocument(definition);
            var smaller = new GridDimensions(3, 3, 2);

            Assert.That(document.CountCellsOutside(smaller), Is.EqualTo(1));
            document.Resize(smaller);
            document.Commit("Test resize");

            Assert.That(
                document.GetFlags(new GridCell(1, 1, 1)),
                Is.EqualTo(BoardCellFlags.SupportsPlacement));
            Assert.That(
                document.GetFlags(new GridCell(4, 4, 3)),
                Is.EqualTo(BoardCellFlags.None));
            Assert.That(document.Dimensions, Is.EqualTo(smaller));
        }

        [Test]
        public void Validate_ReportsOutOfBoundsAndBuildableWithoutSupport()
        {
            BoardDefinition definition = CreateTransientBoard(
                new GridDimensions(2, 2, 2),
                new[]
                {
                    new BoardCellDefinition(new GridCell(3, 0, 0), BoardCellFlags.Buildable)
                });
            var document = new BoardAuthoringDocument(definition);

            string combined = string.Join(" | ", document.Validate());

            StringAssert.Contains("outside", combined.ToLowerInvariant());
            StringAssert.Contains("do not support placement", combined.ToLowerInvariant());
        }

        [Test]
        public void Commit_UndoRedoRestoresOneBoardMutation()
        {
            BoardDefinition definition = CreateTemporaryAsset(
                new GridDimensions(3, 3, 2),
                Array.Empty<BoardCellDefinition>());
            var document = new BoardAuthoringDocument(definition);
            GridCell coordinate = new GridCell(1, 1, 0);

            document.Paint(coordinate, BoardPaintPreset.Buildable);
            document.Commit("Paint test stroke");
            Assert.That(document.GetFlags(coordinate), Is.Not.EqualTo(BoardCellFlags.None));

            Undo.PerformUndo();
            document.Reload();
            Assert.That(document.GetFlags(coordinate), Is.EqualTo(BoardCellFlags.None));

            Undo.PerformRedo();
            document.Reload();
            Assert.That(
                document.GetFlags(coordinate),
                Is.EqualTo(BoardCellFlags.SupportsPlacement | BoardCellFlags.Buildable));
        }

        [Test]
        public void SetCameraGridSpans_ClampsAndCommitsSerializedValues()
        {
            BoardDefinition definition = CreateTransientBoard(
                new GridDimensions(3, 3, 2),
                Array.Empty<BoardCellDefinition>());
            var document = new BoardAuthoringDocument(definition);

            document.SetCameraGridSpans(-4, 6);
            document.Commit("Apply camera limits");

            Assert.That(document.MaxCameraGridXSpan, Is.Zero);
            Assert.That(document.MaxCameraGridYSpan, Is.EqualTo(6));
            Assert.That(definition.MaxCameraGridXSpan, Is.Zero);
            Assert.That(definition.MaxCameraGridYSpan, Is.EqualTo(6));

            var serialized = new SerializedObject(definition);
            serialized.UpdateIfRequiredOrScript();
            Assert.That(serialized.FindProperty("maxCameraGridXSpan").intValue, Is.Zero);
            Assert.That(serialized.FindProperty("maxCameraGridYSpan").intValue, Is.EqualTo(6));
        }

        [Test]
        public void CameraGridSpans_CommitSupportsUndoRedo()
        {
            BoardDefinition definition = CreateTemporaryAsset(
                new GridDimensions(3, 3, 2),
                Array.Empty<BoardCellDefinition>());
            var document = new BoardAuthoringDocument(definition);

            document.SetCameraGridSpans(4, 5);
            document.Commit("Apply camera limits");
            Assert.That(document.MaxCameraGridXSpan, Is.EqualTo(4));
            Assert.That(document.MaxCameraGridYSpan, Is.EqualTo(5));

            Undo.PerformUndo();
            document.Reload();
            Assert.That(document.MaxCameraGridXSpan, Is.Zero);
            Assert.That(document.MaxCameraGridYSpan, Is.Zero);

            Undo.PerformRedo();
            document.Reload();
            Assert.That(document.MaxCameraGridXSpan, Is.EqualTo(4));
            Assert.That(document.MaxCameraGridYSpan, Is.EqualTo(5));
        }

        [Test]
        public void TemporaryAsset_SaveAndReloadPreservesCameraGridSpans()
        {
            BoardDefinition definition = CreateTemporaryAsset(
                new GridDimensions(3, 3, 2),
                Array.Empty<BoardCellDefinition>());
            string path = AssetDatabase.GetAssetPath(definition);
            var document = new BoardAuthoringDocument(definition);

            document.SetCameraGridSpans(7, 8);
            document.Commit("Save camera limits");
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            BoardDefinition reloaded = AssetDatabase.LoadAssetAtPath<BoardDefinition>(path);
            var reloadedDocument = new BoardAuthoringDocument(reloaded);
            Assert.That(reloadedDocument.MaxCameraGridXSpan, Is.EqualTo(7));
            Assert.That(reloadedDocument.MaxCameraGridYSpan, Is.EqualTo(8));
            Assert.That(reloaded.MaxCameraGridXSpan, Is.EqualTo(7));
            Assert.That(reloaded.MaxCameraGridYSpan, Is.EqualTo(8));
        }

        [Test]
        public void TemporaryAsset_SaveAndReloadPreservesPaintedCells()
        {
            BoardDefinition definition = CreateTemporaryAsset(
                new GridDimensions(3, 3, 2),
                Array.Empty<BoardCellDefinition>());
            string path = AssetDatabase.GetAssetPath(definition);
            var document = new BoardAuthoringDocument(definition);
            GridCell coordinate = new GridCell(2, 0, 1);
            document.Paint(coordinate, BoardPaintPreset.VolumeBlocker);
            document.Commit("Save temporary board");
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            BoardDefinition reloaded = AssetDatabase.LoadAssetAtPath<BoardDefinition>(path);
            var reloadedDocument = new BoardAuthoringDocument(reloaded);

            Assert.That(
                reloadedDocument.GetFlags(coordinate),
                Is.EqualTo(BoardCellFlags.StaticBlocker));
        }

        [Test]
        public void PainterOutput_RemainsCompatibleWithGridBoard()
        {
            BoardDefinition definition = CreateTransientBoard(
                new GridDimensions(3, 3, 2),
                Array.Empty<BoardCellDefinition>());
            var document = new BoardAuthoringDocument(definition);
            GridCell coordinate = new GridCell(1, 2, 0);
            document.Paint(coordinate, BoardPaintPreset.Buildable);
            document.Commit("Create compatible board");

            var board = new GridBoard(definition, Vector3.zero);

            Assert.That(board.SupportsPlacement(coordinate), Is.True);
            Assert.That(board.IsBuildable(coordinate), Is.True);
            Assert.That(board.IsStaticBlocker(coordinate), Is.False);
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
            const string parent = "Assets/_Project/GridPlacement/Tests/EditMode";
            if (!AssetDatabase.IsValidFolder(TemporaryFolder))
            {
                AssetDatabase.CreateFolder(parent, "__BoardPainterTemp");
            }
        }

        private static BoardCellDefinition[] GetCells(BoardDefinition definition) =>
            (BoardCellDefinition[])GetField("cells").GetValue(definition);

        private static int CountActiveCells(
            BoardAuthoringDocument document,
            GridDimensions dimensions,
            int level)
        {
            int count = 0;
            for (int z = 0; z < dimensions.Depth; z++)
            {
                for (int x = 0; x < dimensions.Width; x++)
                {
                    if (document.GetFlags(new GridCell(x, z, level))
                        != BoardCellFlags.None)
                    {
                        count++;
                    }
                }
            }

            return count;
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
