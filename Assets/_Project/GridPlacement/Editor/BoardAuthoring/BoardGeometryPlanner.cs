using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace TowerDefense3D.GridPlacement.Editor
{
    internal static class BoardGeometryPlanner
    {
        private static readonly BoardGeometryKind[] OrderedKinds =
        {
            BoardGeometryKind.PlacementSurface,
            BoardGeometryKind.StaticBlocker,
        };

        internal static BoardGeometryPlan Create(BoardDefinition board)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            GridDimensions dimensions = board.Dimensions;
            var rectangles = new List<BoardGeometryRectangle>();

            if (dimensions.Width > 0 && dimensions.Height > 0 && dimensions.Depth > 0)
            {
                BoardCellFlags[] flags = BuildFlags(board.Cells, dimensions);
                BuildRectangles(flags, dimensions, rectangles);
            }

            LowestBoardLevelBounds? focusRegion = null;
            if (LowestBoardLevelBoundsCalculator.TryCalculate(
                    board,
                    out LowestBoardLevelBounds lowestLevelBounds)
                && BoardCameraFocusRegionCalculator.TryCalculate(
                    board,
                    lowestLevelBounds,
                    out LowestBoardLevelBounds focusBounds))
            {
                focusRegion = focusBounds;
            }

            string signature = BuildSignature(
                dimensions,
                board.CellSize,
                board.HeightUnit,
                board.VisualizeInScene,
                rectangles,
                focusRegion);

            return new BoardGeometryPlan(
                board.CellSize,
                board.HeightUnit,
                board.VisualizeInScene,
                rectangles,
                focusRegion,
                signature);
        }

        private static BoardCellFlags[] BuildFlags(
            IReadOnlyList<BoardCellDefinition> cells,
            GridDimensions dimensions)
        {
            var flags = new BoardCellFlags[checked(
                dimensions.Width * dimensions.Height * dimensions.Depth)];

            if (cells == null)
            {
                return flags;
            }

            for (int index = 0; index < cells.Count; index++)
            {
                BoardCellDefinition cellDefinition = cells[index];
                GridCell cell = cellDefinition.Coordinate;
                if (!IsWithinBounds(cell, dimensions))
                {
                    continue;
                }

                int flatIndex = GetFlatIndex(cell.X, cell.Y, cell.Z, dimensions);
                flags[flatIndex] |= cellDefinition.Flags;
            }

            return flags;
        }

        private static void BuildRectangles(
            BoardCellFlags[] flags,
            GridDimensions dimensions,
            List<BoardGeometryRectangle> rectangles)
        {
            int layerCellCount = checked(dimensions.Width * dimensions.Depth);

            for (int y = 0; y < dimensions.Height; y++)
            {
                for (int kindIndex = 0; kindIndex < OrderedKinds.Length; kindIndex++)
                {
                    BoardGeometryKind kind = OrderedKinds[kindIndex];
                    BoardCellFlags requiredFlag = GetRequiredFlag(kind);
                    var visited = new bool[layerCellCount];

                    for (int z = 0; z < dimensions.Depth; z++)
                    {
                        for (int x = 0; x < dimensions.Width; x++)
                        {
                            if (!CanUse(flags, visited, dimensions, x, y, z, requiredFlag))
                            {
                                continue;
                            }

                            int width = MeasureWidth(
                                flags,
                                visited,
                                dimensions,
                                x,
                                y,
                                z,
                                requiredFlag);
                            int depth = MeasureDepth(
                                flags,
                                visited,
                                dimensions,
                                x,
                                y,
                                z,
                                width,
                                requiredFlag);

                            MarkVisited(visited, dimensions.Width, x, z, width, depth);
                            rectangles.Add(new BoardGeometryRectangle(kind, x, y, z, width, depth));
                        }
                    }
                }
            }
        }

        private static int MeasureWidth(
            BoardCellFlags[] flags,
            bool[] visited,
            GridDimensions dimensions,
            int x,
            int y,
            int z,
            BoardCellFlags requiredFlag)
        {
            int width = 1;
            while (x + width < dimensions.Width
                   && CanUse(flags, visited, dimensions, x + width, y, z, requiredFlag))
            {
                width++;
            }

            return width;
        }

        private static int MeasureDepth(
            BoardCellFlags[] flags,
            bool[] visited,
            GridDimensions dimensions,
            int x,
            int y,
            int z,
            int width,
            BoardCellFlags requiredFlag)
        {
            int depth = 1;
            while (z + depth < dimensions.Depth)
            {
                for (int offsetX = 0; offsetX < width; offsetX++)
                {
                    if (!CanUse(
                            flags,
                            visited,
                            dimensions,
                            x + offsetX,
                            y,
                            z + depth,
                            requiredFlag))
                    {
                        return depth;
                    }
                }

                depth++;
            }

            return depth;
        }

        private static bool CanUse(
            BoardCellFlags[] flags,
            bool[] visited,
            GridDimensions dimensions,
            int x,
            int y,
            int z,
            BoardCellFlags requiredFlag)
        {
            int layerIndex = z * dimensions.Width + x;
            int flatIndex = GetFlatIndex(x, y, z, dimensions);
            return !visited[layerIndex] && (flags[flatIndex] & requiredFlag) != 0;
        }

        private static void MarkVisited(
            bool[] visited,
            int boardWidth,
            int x,
            int z,
            int width,
            int depth)
        {
            for (int offsetZ = 0; offsetZ < depth; offsetZ++)
            {
                int rowStart = (z + offsetZ) * boardWidth + x;
                for (int offsetX = 0; offsetX < width; offsetX++)
                {
                    visited[rowStart + offsetX] = true;
                }
            }
        }

        private static BoardCellFlags GetRequiredFlag(BoardGeometryKind kind)
        {
            return kind == BoardGeometryKind.PlacementSurface
                ? BoardCellFlags.SupportsPlacement
                : BoardCellFlags.StaticBlocker;
        }

        private static bool IsWithinBounds(GridCell cell, GridDimensions dimensions)
        {
            return cell.X >= 0 && cell.X < dimensions.Width
                               && cell.Y >= 0 && cell.Y < dimensions.Height
                               && cell.Z >= 0 && cell.Z < dimensions.Depth;
        }

        private static int GetFlatIndex(int x, int y, int z, GridDimensions dimensions)
        {
            return (y * dimensions.Depth + z) * dimensions.Width + x;
        }

        private static string BuildSignature(
            GridDimensions dimensions,
            float cellSize,
            float heightUnit,
            bool visualizeInScene,
            IReadOnlyList<BoardGeometryRectangle> rectangles,
            LowestBoardLevelBounds? focusRegion)
        {
            var canonical = new StringBuilder();
            canonical.Append(dimensions.Width).Append('|')
                .Append(dimensions.Height).Append('|')
                .Append(dimensions.Depth).Append('|')
                .Append(cellSize.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                .Append(heightUnit.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                .Append(visualizeInScene ? '1' : '0').Append('|');

            if (focusRegion.HasValue)
            {
                LowestBoardLevelBounds region = focusRegion.Value;
                canonical.Append("focus:")
                    .Append(region.Level).Append(',')
                    .Append(region.MinX).Append(',')
                    .Append(region.MinZ).Append(',')
                    .Append(region.MaxXExclusive).Append(',')
                    .Append(region.MaxZExclusive);
            }
            else
            {
                canonical.Append("nofocus");
            }

            for (int index = 0; index < rectangles.Count; index++)
            {
                BoardGeometryRectangle rectangle = rectangles[index];
                canonical.Append(';')
                    .Append((int)rectangle.Kind).Append(',')
                    .Append(rectangle.X).Append(',')
                    .Append(rectangle.Y).Append(',')
                    .Append(rectangle.Z).Append(',')
                    .Append(rectangle.Width).Append(',')
                    .Append(rectangle.Depth);
            }

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()));
                var signature = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                {
                    signature.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
                }

                return signature.ToString();
            }
        }
    }
}
