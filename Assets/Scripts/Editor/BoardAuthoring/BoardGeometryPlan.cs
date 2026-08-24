using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.GridPlacement.Editor
{
    internal enum BoardGeometryKind
    {
        PlacementSurface,
        StaticBlocker,
        RoadSurface,
        RoadSpawnSurface,
        RoadEndSurface,
    }

    internal readonly struct BoardGeometryRectangle
    {
        internal BoardGeometryRectangle(
            BoardGeometryKind kind,
            int x,
            int y,
            int z,
            int width,
            int depth)
        {
            Kind = kind;
            X = x;
            Y = y;
            Z = z;
            Width = width;
            Depth = depth;
        }

        internal BoardGeometryKind Kind { get; }

        internal int X { get; }

        internal int Y { get; }

        internal int Z { get; }

        internal int Width { get; }

        internal int Depth { get; }
    }

    internal readonly struct BoardGridPlaceableVisual
    {
        internal BoardGridPlaceableVisual(
            GridCell coordinate,
            GameObject prefab,
            string displayName,
            GridPlaceableTopology topology,
            GridPlaceableAxis axis,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            int sortingOrder)
        {
            Coordinate = coordinate;
            Prefab = prefab;
            DisplayName = displayName;
            Topology = topology;
            Axis = axis;
            LocalPosition = localPosition;
            LocalRotation = localRotation;
            LocalScale = localScale;
            SortingOrder = sortingOrder;
        }

        internal GridCell Coordinate { get; }
        internal GameObject Prefab { get; }
        internal string DisplayName { get; }
        internal GridPlaceableTopology Topology { get; }
        internal GridPlaceableAxis Axis { get; }
        internal Vector3 LocalPosition { get; }
        internal Quaternion LocalRotation { get; }
        internal Vector3 LocalScale { get; }
        internal int SortingOrder { get; }
    }

    internal sealed class BoardGeometryPlan
    {
        internal BoardGeometryPlan(
            float cellSize,
            float heightUnit,
            bool visualizeInScene,
            IReadOnlyList<BoardGeometryRectangle> rectangles,
            IReadOnlyList<BoardGridPlaceableVisual> gridPlaceableVisuals,
            LowestBoardLevelBounds? focusRegion,
            string signature,
            string gridPlaceableSignature)
        {
            CellSize = cellSize;
            HeightUnit = heightUnit;
            VisualizeInScene = visualizeInScene;
            Rectangles = rectangles ?? throw new ArgumentNullException(nameof(rectangles));
            GridPlaceableVisuals = gridPlaceableVisuals
                ?? throw new ArgumentNullException(nameof(gridPlaceableVisuals));
            FocusRegion = focusRegion;
            Signature = signature ?? throw new ArgumentNullException(nameof(signature));
            GridPlaceableSignature = gridPlaceableSignature
                ?? throw new ArgumentNullException(nameof(gridPlaceableSignature));
        }

        internal float CellSize { get; }

        internal float HeightUnit { get; }

        internal bool VisualizeInScene { get; }

        internal IReadOnlyList<BoardGeometryRectangle> Rectangles { get; }

        internal IReadOnlyList<BoardGridPlaceableVisual>
            GridPlaceableVisuals { get; }

        /// <summary>
        /// The authored Camera Focus region at the lowest playable level, when
        /// one or more cells there carry <see cref="BoardCellFlags.CameraFocus"/>.
        /// Null when no such cell exists, so no overlay is generated.
        /// </summary>
        internal LowestBoardLevelBounds? FocusRegion { get; }

        internal string Signature { get; }

        internal string GridPlaceableSignature { get; }
    }
}
