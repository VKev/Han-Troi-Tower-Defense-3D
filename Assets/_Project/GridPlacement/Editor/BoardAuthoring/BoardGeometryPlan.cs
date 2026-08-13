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
            string signature)
        {
            CellSize = cellSize;
            HeightUnit = heightUnit;
            VisualizeInScene = visualizeInScene;
            Rectangles = rectangles ?? throw new ArgumentNullException(nameof(rectangles));
            Signature = signature ?? throw new ArgumentNullException(nameof(signature));
        }

        internal float CellSize { get; }

        internal float HeightUnit { get; }

        internal bool VisualizeInScene { get; }

        internal IReadOnlyList<BoardGeometryRectangle> Rectangles { get; }

        internal string Signature { get; }
    }
}
