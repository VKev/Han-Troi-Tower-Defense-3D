using System;
using System.Collections.Generic;
using TowerDefense3D.GridPlacement;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.GridPlacement.Editor
{
    public sealed class BoardAuthoringDocument
    {
        private const string DimensionsProperty = "dimensions";
        private const string CellSizeProperty = "cellSize";
        private const string HeightUnitProperty = "heightUnit";
        private const string MaxCameraGridXSpanProperty = "maxCameraGridXSpan";
        private const string MaxCameraGridYSpanProperty = "maxCameraGridYSpan";
        private const string CellsProperty = "cells";

        private readonly Dictionary<GridCell, BoardCellFlags> cells =
            new Dictionary<GridCell, BoardCellFlags>();

        public BoardAuthoringDocument(BoardDefinition asset)
        {
            Asset = asset != null ? asset : throw new ArgumentNullException(nameof(asset));
            Reload();
        }

        public BoardDefinition Asset { get; }
        public GridDimensions Dimensions { get; private set; }
        public float CellSize { get; private set; }
        public float HeightUnit { get; private set; }
        public int MaxCameraGridXSpan { get; private set; }
        public int MaxCameraGridYSpan { get; private set; }
        public int DuplicateCoordinateCount { get; private set; }
        public int SerializedNoneEntryCount { get; private set; }
        public int ActiveCellCount => cells.Count;

        public void Reload()
        {
            cells.Clear();
            DuplicateCoordinateCount = 0;
            SerializedNoneEntryCount = 0;

            var serialized = new SerializedObject(Asset);
            serialized.UpdateIfRequiredOrScript();
            Dimensions = ReadDimensions(serialized.FindProperty(DimensionsProperty));
            CellSize = serialized.FindProperty(CellSizeProperty).floatValue;
            HeightUnit = serialized.FindProperty(HeightUnitProperty).floatValue;
            MaxCameraGridXSpan = Mathf.Max(
                0,
                serialized.FindProperty(MaxCameraGridXSpanProperty).intValue);
            MaxCameraGridYSpan = Mathf.Max(
                0,
                serialized.FindProperty(MaxCameraGridYSpanProperty).intValue);

            SerializedProperty serializedCells = serialized.FindProperty(CellsProperty);
            for (int i = 0; i < serializedCells.arraySize; i++)
            {
                SerializedProperty element = serializedCells.GetArrayElementAtIndex(i);
                GridCell coordinate = ReadCoordinate(element.FindPropertyRelative("coordinate"));
                BoardCellFlags flags =
                    (BoardCellFlags)element.FindPropertyRelative("flags").intValue;

                if (flags == BoardCellFlags.None)
                {
                    SerializedNoneEntryCount++;
                    continue;
                }

                if (cells.TryGetValue(coordinate, out BoardCellFlags existing))
                {
                    cells[coordinate] = existing | flags;
                    DuplicateCoordinateCount++;
                }
                else
                {
                    cells.Add(coordinate, flags);
                }
            }
        }

        public BoardCellFlags GetFlags(GridCell coordinate) =>
            cells.TryGetValue(coordinate, out BoardCellFlags flags)
                ? flags
                : BoardCellFlags.None;

        public void Paint(GridCell coordinate, BoardPaintPreset preset)
        {
            BoardCellFlags flags = BoardPaintPresetUtility.GetFlags(preset);
            if (flags == BoardCellFlags.None)
            {
                cells.Remove(coordinate);
            }
            else
            {
                cells[coordinate] = flags;
            }
        }

        public void SetCameraFocus(GridCell coordinate, bool enabled)
        {
            BoardCellFlags current = GetFlags(coordinate);
            BoardCellFlags updated = enabled
                ? current | BoardCellFlags.CameraFocus
                : current & ~BoardCellFlags.CameraFocus;

            if (updated == BoardCellFlags.None)
            {
                cells.Remove(coordinate);
            }
            else
            {
                cells[coordinate] = updated;
            }
        }

        public bool TryGetLowestPlayableLevel(out int level)
        {
            bool found = false;
            int lowest = 0;
            foreach (KeyValuePair<GridCell, BoardCellFlags> pair in cells)
            {
                if ((pair.Value & BoardCellFlags.SupportsPlacement) == 0)
                {
                    continue;
                }

                if (!found || pair.Key.Y < lowest)
                {
                    lowest = pair.Key.Y;
                    found = true;
                }
            }

            level = found ? lowest : 0;
            return found;
        }

        public void SetMetrics(float cellSize, float heightUnit)
        {
            CellSize = Mathf.Max(0.01f, cellSize);
            HeightUnit = Mathf.Max(0.01f, heightUnit);
        }

        public void SetCameraGridSpans(int maxCameraGridXSpan, int maxCameraGridYSpan)
        {
            MaxCameraGridXSpan = Mathf.Max(0, maxCameraGridXSpan);
            MaxCameraGridYSpan = Mathf.Max(0, maxCameraGridYSpan);
        }

        public int CountCellsOutside(GridDimensions dimensions)
        {
            int count = 0;
            foreach (GridCell coordinate in cells.Keys)
            {
                if (!IsWithinBounds(coordinate, dimensions))
                {
                    count++;
                }
            }

            return count;
        }

        public void Resize(GridDimensions dimensions)
        {
            if (dimensions.Width <= 0 || dimensions.Depth <= 0 || dimensions.Height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(dimensions));
            }

            var toRemove = new List<GridCell>();
            foreach (GridCell coordinate in cells.Keys)
            {
                if (!IsWithinBounds(coordinate, dimensions))
                {
                    toRemove.Add(coordinate);
                }
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                cells.Remove(toRemove[i]);
            }

            Dimensions = dimensions;
        }

        public IReadOnlyList<string> Validate()
        {
            var issues = new List<string>();
            if (Dimensions.Width <= 0 || Dimensions.Depth <= 0 || Dimensions.Height <= 0)
            {
                issues.Add("Board dimensions must be positive.");
            }
            else
            {
                long volume = (long)Dimensions.Width * Dimensions.Depth * Dimensions.Height;
                if (volume > 1_000_000L)
                {
                    issues.Add($"Large board volume ({volume:N0} cells) increases runtime memory.");
                }
            }

            if (CellSize <= 0f || HeightUnit <= 0f)
            {
                issues.Add("Cell Size and Height Unit must be positive.");
            }

            if (DuplicateCoordinateCount > 0)
            {
                issues.Add($"{DuplicateCoordinateCount} duplicate coordinate entries will be merged.");
            }

            if (SerializedNoneEntryCount > 0)
            {
                issues.Add($"{SerializedNoneEntryCount} inactive serialized entries will be removed on save.");
            }

            int outsideCount = 0;
            int buildableWithoutSupport = 0;
            int unknownFlagCount = 0;
            const BoardCellFlags knownFlags = BoardCellFlags.SupportsPlacement
                | BoardCellFlags.Buildable
                | BoardCellFlags.StaticBlocker
                | BoardCellFlags.CameraFocus;

            foreach (KeyValuePair<GridCell, BoardCellFlags> pair in cells)
            {
                if (!IsWithinBounds(pair.Key, Dimensions))
                {
                    outsideCount++;
                }

                if ((pair.Value & BoardCellFlags.Buildable) != 0
                    && (pair.Value & BoardCellFlags.SupportsPlacement) == 0)
                {
                    buildableWithoutSupport++;
                }

                if ((pair.Value & ~knownFlags) != 0)
                {
                    unknownFlagCount++;
                }
            }

            if (outsideCount > 0)
            {
                issues.Add($"{outsideCount} authored cells are outside the current dimensions.");
            }

            if (buildableWithoutSupport > 0)
            {
                issues.Add($"{buildableWithoutSupport} buildable cells do not support placement.");
            }

            if (unknownFlagCount > 0)
            {
                issues.Add($"{unknownFlagCount} cells contain unknown flag bits.");
            }

            return issues;
        }

        public void Commit(string undoName)
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            Undo.RegisterCompleteObjectUndo(Asset, undoName);

            var serialized = new SerializedObject(Asset);
            serialized.Update();
            WriteDimensions(serialized.FindProperty(DimensionsProperty), Dimensions);
            serialized.FindProperty(CellSizeProperty).floatValue = CellSize;
            serialized.FindProperty(HeightUnitProperty).floatValue = HeightUnit;
            serialized.FindProperty(MaxCameraGridXSpanProperty).intValue = MaxCameraGridXSpan;
            serialized.FindProperty(MaxCameraGridYSpanProperty).intValue = MaxCameraGridYSpan;

            List<KeyValuePair<GridCell, BoardCellFlags>> ordered = GetOrderedCells();
            SerializedProperty serializedCells = serialized.FindProperty(CellsProperty);
            serializedCells.arraySize = ordered.Count;
            for (int i = 0; i < ordered.Count; i++)
            {
                SerializedProperty element = serializedCells.GetArrayElementAtIndex(i);
                WriteCoordinate(element.FindPropertyRelative("coordinate"), ordered[i].Key);
                element.FindPropertyRelative("flags").intValue = (int)ordered[i].Value;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(Asset);
            Undo.CollapseUndoOperations(undoGroup);
            Reload();
            BoardChangeScheduler.Queue(Asset);
        }

        private List<KeyValuePair<GridCell, BoardCellFlags>> GetOrderedCells()
        {
            var ordered = new List<KeyValuePair<GridCell, BoardCellFlags>>(cells.Count);
            foreach (KeyValuePair<GridCell, BoardCellFlags> pair in cells)
            {
                if (pair.Value != BoardCellFlags.None)
                {
                    ordered.Add(pair);
                }
            }

            ordered.Sort((left, right) =>
            {
                int comparison = left.Key.Y.CompareTo(right.Key.Y);
                if (comparison != 0)
                {
                    return comparison;
                }

                comparison = left.Key.Z.CompareTo(right.Key.Z);
                return comparison != 0
                    ? comparison
                    : left.Key.X.CompareTo(right.Key.X);
            });
            return ordered;
        }

        private static bool IsWithinBounds(GridCell coordinate, GridDimensions dimensions) =>
            coordinate.X >= 0 && coordinate.X < dimensions.Width
            && coordinate.Z >= 0 && coordinate.Z < dimensions.Depth
            && coordinate.Y >= 0 && coordinate.Y < dimensions.Height;

        private static GridDimensions ReadDimensions(SerializedProperty property) =>
            new GridDimensions(
                property.FindPropertyRelative("width").intValue,
                property.FindPropertyRelative("depth").intValue,
                property.FindPropertyRelative("height").intValue);

        private static void WriteDimensions(SerializedProperty property, GridDimensions value)
        {
            property.FindPropertyRelative("width").intValue = value.Width;
            property.FindPropertyRelative("depth").intValue = value.Depth;
            property.FindPropertyRelative("height").intValue = value.Height;
        }

        private static GridCell ReadCoordinate(SerializedProperty property) =>
            new GridCell(
                property.FindPropertyRelative("x").intValue,
                property.FindPropertyRelative("z").intValue,
                property.FindPropertyRelative("y").intValue);

        private static void WriteCoordinate(SerializedProperty property, GridCell value)
        {
            property.FindPropertyRelative("x").intValue = value.X;
            property.FindPropertyRelative("z").intValue = value.Z;
            property.FindPropertyRelative("y").intValue = value.Y;
        }
    }
}
