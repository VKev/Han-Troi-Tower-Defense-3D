using System;
using System.Collections.Generic;

namespace TowerDefense3D.GridPlacement.Editor
{
    internal enum BoardGeometryKind
    {
        PlacementSurface,
        StaticBlocker,
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

    internal sealed class BoardGeometryPlan
    {
        internal BoardGeometryPlan(
            float cellSize,
            float heightUnit,
            bool visualizeInScene,
            IReadOnlyList<BoardGeometryRectangle> rectangles,
            LowestBoardLevelBounds? focusRegion,
            string signature)
        {
            CellSize = cellSize;
            HeightUnit = heightUnit;
            VisualizeInScene = visualizeInScene;
            Rectangles = rectangles ?? throw new ArgumentNullException(nameof(rectangles));
            FocusRegion = focusRegion;
            Signature = signature ?? throw new ArgumentNullException(nameof(signature));
        }

        internal float CellSize { get; }

        internal float HeightUnit { get; }

        internal bool VisualizeInScene { get; }

        internal IReadOnlyList<BoardGeometryRectangle> Rectangles { get; }

        /// <summary>
        /// The authored Camera Focus region at the lowest playable level, when
        /// one or more cells there carry <see cref="BoardCellFlags.CameraFocus"/>.
        /// Null when no such cell exists, so no overlay is generated.
        /// </summary>
        internal LowestBoardLevelBounds? FocusRegion { get; }

        internal string Signature { get; }
    }
}
