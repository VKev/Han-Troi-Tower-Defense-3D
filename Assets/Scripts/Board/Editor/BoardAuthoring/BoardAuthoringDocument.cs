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
        private const string GridPlaceablesProperty = "gridPlaceables";

        private readonly Dictionary<GridCell, BoardCellFlags> cells =
            new Dictionary<GridCell, BoardCellFlags>();
        private readonly Dictionary<GridCell, GameObject> gridPlaceables =
            new Dictionary<GridCell, GameObject>();

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
        public int DuplicateGridPlaceableCoordinateCount { get; private set; }
        public int InvalidGridPlaceableCount { get; private set; }
        public int ActiveCellCount => cells.Count;
        public int ActiveGridPlaceableCount => gridPlaceables.Count;

        public void Reload()
        {
            cells.Clear();
            gridPlaceables.Clear();
            DuplicateCoordinateCount = 0;
            SerializedNoneEntryCount = 0;
            DuplicateGridPlaceableCoordinateCount = 0;
            InvalidGridPlaceableCount = 0;

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

            SerializedProperty serializedGridPlaceables =
                serialized.FindProperty(GridPlaceablesProperty);
            for (int i = 0;
                 serializedGridPlaceables != null
                 && i < serializedGridPlaceables.arraySize;
                 i++)
            {
                SerializedProperty element =
                    serializedGridPlaceables.GetArrayElementAtIndex(i);
                GridCell coordinate = ReadCoordinate(
                    element.FindPropertyRelative("coordinate"));
                var prefab = element.FindPropertyRelative("prefab")
                    .objectReferenceValue as GameObject;
                if (!IsValidGridPlaceablePrefab(prefab))
                {
                    InvalidGridPlaceableCount++;
                    continue;
                }

                if (gridPlaceables.ContainsKey(coordinate))
                {
                    DuplicateGridPlaceableCoordinateCount++;
                }

                gridPlaceables[coordinate] = prefab;
            }
        }

        public BoardCellFlags GetFlags(GridCell coordinate) =>
            cells.TryGetValue(coordinate, out BoardCellFlags flags)
                ? flags
                : BoardCellFlags.None;

        public GameObject GetGridPlaceable(GridCell coordinate) =>
            gridPlaceables.TryGetValue(coordinate, out GameObject prefab)
                ? prefab
                : null;

        public void SetGridPlaceable(GridCell coordinate, GameObject prefab)
        {
            if (prefab == null)
            {
                gridPlaceables.Remove(coordinate);
                return;
            }

            if (!IsValidGridPlaceablePrefab(prefab))
            {
                throw new ArgumentException(
                    "Grid placeable must be a prefab asset whose root has a GridPlaceableAuthoring component.",
                    nameof(prefab));
            }

            gridPlaceables[coordinate] = prefab;
        }

        public void Paint(GridCell coordinate, BoardPaintPreset preset)
        {
            BoardCellFlags current = GetFlags(coordinate);
            BoardCellFlags updated =
                (current & ~BoardPaintPresetUtility.BasicCellMask)
                | BoardPaintPresetUtility.GetFlags(preset);
            if (updated == BoardCellFlags.None)
            {
                cells.Remove(coordinate);
            }
            else
            {
                cells[coordinate] = updated;
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

        public void SetRoadRole(GridCell coordinate, RoadPaintMode mode)
        {
            BoardCellFlags current = GetFlags(coordinate);
            BoardCellFlags updated =
                (current & ~RoadPaintModeUtility.RoadRoleMask) | RoadPaintModeUtility.GetFlags(mode);

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

            foreach (GridCell coordinate in gridPlaceables.Keys)
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

            toRemove.Clear();
            foreach (GridCell coordinate in gridPlaceables.Keys)
            {
                if (!IsWithinBounds(coordinate, dimensions))
                {
                    toRemove.Add(coordinate);
                }
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                gridPlaceables.Remove(toRemove[i]);
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

            if (DuplicateGridPlaceableCoordinateCount > 0)
            {
                issues.Add(
                    $"{DuplicateGridPlaceableCoordinateCount} duplicate prefab coordinates "
                    + "will be replaced by their last entry.");
            }

            if (InvalidGridPlaceableCount > 0)
            {
                issues.Add(
                    $"{InvalidGridPlaceableCount} prefab entries are missing a valid root "
                    + "GridPlaceableAuthoring component and will be removed on save.");
            }

            int outsideCount = 0;
            int buildableWithoutSupport = 0;
            int unknownFlagCount = 0;
            int roadCellCount = 0;
            int roadSpawnCount = 0;
            int roadEndCount = 0;
            const BoardCellFlags knownFlags = BoardCellFlags.SupportsPlacement
                | BoardCellFlags.Buildable
                | BoardCellFlags.StaticBlocker
                | BoardCellFlags.CameraFocus
                | BoardCellFlags.Road
                | BoardCellFlags.RoadSpawn
                | BoardCellFlags.RoadEnd;

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

                if ((pair.Value & RoadPaintModeUtility.RoadRoleMask) != 0)
                {
                    roadCellCount++;

                    if ((pair.Value & BoardCellFlags.RoadSpawn) != 0)
                    {
                        roadSpawnCount++;
                    }

                    if ((pair.Value & BoardCellFlags.RoadEnd) != 0)
                    {
                        roadEndCount++;
                    }
                }
            }

            if (outsideCount > 0)
            {
                issues.Add($"{outsideCount} authored cells are outside the current dimensions.");
            }

            int outsideGridPlaceableCount = 0;
            foreach (GridCell coordinate in gridPlaceables.Keys)
            {
                if (!IsWithinBounds(coordinate, Dimensions))
                {
                    outsideGridPlaceableCount++;
                }
            }

            if (outsideGridPlaceableCount > 0)
            {
                issues.Add(
                    $"{outsideGridPlaceableCount} prefab cells are outside the current dimensions.");
            }

            if (buildableWithoutSupport > 0)
            {
                issues.Add($"{buildableWithoutSupport} buildable cells do not support placement.");
            }

            if (unknownFlagCount > 0)
            {
                issues.Add($"{unknownFlagCount} cells contain unknown flag bits.");
            }

            if (roadCellCount > 0 && roadSpawnCount == 0)
            {
                issues.Add("Board has Road cells but no Road Spawn cell.");
            }

            if (roadCellCount > 0 && roadEndCount == 0)
            {
                issues.Add("Board has Road cells but no Road End cell.");
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

            List<KeyValuePair<GridCell, GameObject>> orderedGridPlaceables =
                GetOrderedGridPlaceables();
            SerializedProperty serializedGridPlaceables =
                serialized.FindProperty(GridPlaceablesProperty);
            serializedGridPlaceables.arraySize = orderedGridPlaceables.Count;
            for (int i = 0; i < orderedGridPlaceables.Count; i++)
            {
                SerializedProperty element =
                    serializedGridPlaceables.GetArrayElementAtIndex(i);
                WriteCoordinate(
                    element.FindPropertyRelative("coordinate"),
                    orderedGridPlaceables[i].Key);
                element.FindPropertyRelative("prefab").objectReferenceValue =
                    orderedGridPlaceables[i].Value;
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

        private List<KeyValuePair<GridCell, GameObject>>
            GetOrderedGridPlaceables()
        {
            var ordered =
                new List<KeyValuePair<GridCell, GameObject>>(
                    gridPlaceables.Count);
            foreach (KeyValuePair<GridCell, GameObject> pair in gridPlaceables)
            {
                if (IsValidGridPlaceablePrefab(pair.Value))
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

        private static bool IsValidGridPlaceablePrefab(GameObject prefab) =>
            prefab != null
            && PrefabUtility.IsPartOfPrefabAsset(prefab)
            && prefab.GetComponent<GridPlaceableAuthoring>() != null;

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
