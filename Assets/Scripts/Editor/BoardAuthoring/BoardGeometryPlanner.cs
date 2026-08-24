using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.GridPlacement.Editor
{
    internal static class BoardGeometryPlanner
    {
        private static readonly BoardGeometryKind[] OrderedKinds =
        {
            BoardGeometryKind.PlacementSurface,
            BoardGeometryKind.StaticBlocker,
            BoardGeometryKind.RoadSurface,
            BoardGeometryKind.RoadSpawnSurface,
            BoardGeometryKind.RoadEndSurface,
        };

        private const int NegativeXNeighbor = 1 << 0;
        private const int PositiveXNeighbor = 1 << 1;
        private const int NegativeZNeighbor = 1 << 2;
        private const int PositiveZNeighbor = 1 << 3;
        private const int AllNeighbors = NegativeXNeighbor
                                         | PositiveXNeighbor
                                         | NegativeZNeighbor
                                         | PositiveZNeighbor;

        internal static BoardGeometryPlan Create(BoardDefinition board)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            GridDimensions dimensions = board.Dimensions;
            var rectangles = new List<BoardGeometryRectangle>();
            var gridPlaceableVisuals =
                new List<BoardGridPlaceableVisual>();

            if (dimensions.Width > 0 && dimensions.Height > 0 && dimensions.Depth > 0)
            {
                BoardCellFlags[] flags = BuildFlags(board.Cells, dimensions);
                BuildRectangles(flags, dimensions, rectangles);
                BuildGridPlaceableVisuals(
                    board.GridPlaceables,
                    dimensions,
                    board.CellSize,
                    board.HeightUnit,
                    gridPlaceableVisuals);
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
            string gridPlaceableSignature = BuildGridPlaceableSignature(
                dimensions,
                board.CellSize,
                board.HeightUnit,
                gridPlaceableVisuals);

            return new BoardGeometryPlan(
                board.CellSize,
                board.HeightUnit,
                board.VisualizeInScene,
                rectangles,
                gridPlaceableVisuals,
                focusRegion,
                signature,
                gridPlaceableSignature);
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

        private static void BuildGridPlaceableVisuals(
            IReadOnlyList<GridPlaceablePlacement> placements,
            GridDimensions dimensions,
            float cellSize,
            float heightUnit,
            List<BoardGridPlaceableVisual> visuals)
        {
            var byCoordinate = new Dictionary<GridCell, GameObject>();
            if (placements != null)
            {
                for (int index = 0; index < placements.Count; index++)
                {
                    GridPlaceablePlacement placement = placements[index];
                    if (!IsWithinBounds(placement.Coordinate, dimensions)
                        || placement.Prefab == null
                        || placement.Prefab.GetComponent<GridPlaceableAuthoring>() == null)
                    {
                        continue;
                    }

                    byCoordinate[placement.Coordinate] = placement.Prefab;
                }
            }

            var ordered =
                new List<KeyValuePair<GridCell, GameObject>>(byCoordinate);
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

            for (int index = 0; index < ordered.Count; index++)
            {
                GridCell coordinate = ordered[index].Key;
                GameObject prefab = ordered[index].Value;
                GridPlaceableAuthoring placeable = prefab.GetComponent<GridPlaceableAuthoring>();
                GridPlaceableTopology topology =
                    GridPlaceableTopology.Isolated;
                GridPlaceableAxis axis = placeable.IsolatedAxis;
                float topologyRotation = 0f;

                if (placeable.RotationMode
                    == GridPlaceableRotationMode.StraightAlongMatchingNeighbors)
                {
                    int neighborMask = GetMatchingNeighborMask(
                        byCoordinate,
                        coordinate,
                        prefab);
                    topology = GetTopology(neighborMask);
                    axis = GetAxis(neighborMask, placeable.IsolatedAxis);
                    topologyRotation = GetTopologyRotation(
                        topology,
                        neighborMask,
                        axis);
                }

                GameObject visualPrefab =
                    placeable.GetVisualPrefab(topology);
                if (visualPrefab == null)
                {
                    continue;
                }

                Vector3 offset = placeable.CellOffset;
                Vector3 localPosition = new Vector3(
                    (coordinate.X + 0.5f + offset.x) * cellSize,
                    (coordinate.Y + offset.y) * heightUnit,
                    (coordinate.Z + 0.5f + offset.z) * cellSize);
                Vector3 eulerAngles = placeable.BaseEulerAngles;
                if (placeable.RotationMode
                    == GridPlaceableRotationMode.StraightAlongMatchingNeighbors)
                {
                    eulerAngles.y += topologyRotation;
                }

                visuals.Add(new BoardGridPlaceableVisual(
                    coordinate,
                    visualPrefab,
                    placeable.DisplayName,
                    topology,
                    axis,
                    localPosition,
                    Quaternion.Euler(eulerAngles),
                    Vector3.Scale(
                        Vector3.one * cellSize,
                        placeable.ScaleMultiplier),
                    placeable.RendererSortingOrder));
            }
        }

        private static int GetMatchingNeighborMask(
            IReadOnlyDictionary<GridCell, GameObject> placements,
            GridCell coordinate,
            GameObject prefab)
        {
            int mask = 0;
            if (HasMatchingNeighbor(placements, coordinate, prefab, -1, 0))
            {
                mask |= NegativeXNeighbor;
            }

            if (HasMatchingNeighbor(placements, coordinate, prefab, 1, 0))
            {
                mask |= PositiveXNeighbor;
            }

            if (HasMatchingNeighbor(placements, coordinate, prefab, 0, -1))
            {
                mask |= NegativeZNeighbor;
            }

            if (HasMatchingNeighbor(placements, coordinate, prefab, 0, 1))
            {
                mask |= PositiveZNeighbor;
            }

            return mask;
        }

        private static GridPlaceableTopology GetTopology(int neighborMask)
        {
            int neighborCount = 0;
            for (int remaining = neighborMask;
                 remaining != 0;
                 remaining >>= 1)
            {
                neighborCount += remaining & 1;
            }

            switch (neighborCount)
            {
                case 0:
                    return GridPlaceableTopology.Isolated;
                case 1:
                    return GridPlaceableTopology.End;
                case 2:
                    return neighborMask == (NegativeXNeighbor | PositiveXNeighbor)
                           || neighborMask
                           == (NegativeZNeighbor | PositiveZNeighbor)
                        ? GridPlaceableTopology.Straight
                        : GridPlaceableTopology.Corner;
                case 3:
                    return GridPlaceableTopology.ThreeWay;
                default:
                    return GridPlaceableTopology.FourWay;
            }
        }

        private static GridPlaceableAxis GetAxis(
            int neighborMask,
            GridPlaceableAxis isolatedAxis)
        {
            bool hasX = (neighborMask
                         & (NegativeXNeighbor | PositiveXNeighbor)) != 0;
            bool hasZ = (neighborMask
                         & (NegativeZNeighbor | PositiveZNeighbor)) != 0;
            return hasZ && !hasX ? GridPlaceableAxis.Z
                : hasX && !hasZ ? GridPlaceableAxis.X
                : isolatedAxis;
        }

        private static float GetTopologyRotation(
            GridPlaceableTopology topology,
            int neighborMask,
            GridPlaceableAxis axis)
        {
            switch (topology)
            {
                case GridPlaceableTopology.End:
                case GridPlaceableTopology.Straight:
                    return axis == GridPlaceableAxis.Z ? 90f : 0f;
                case GridPlaceableTopology.Corner:
                    if (neighborMask
                        == (PositiveXNeighbor | NegativeZNeighbor))
                    {
                        return 90f;
                    }

                    if (neighborMask
                        == (NegativeXNeighbor | NegativeZNeighbor))
                    {
                        return 180f;
                    }

                    return neighborMask
                           == (NegativeXNeighbor | PositiveZNeighbor)
                        ? 270f
                        : 0f;
                case GridPlaceableTopology.ThreeWay:
                    int missingNeighbor = AllNeighbors & ~neighborMask;
                    if (missingNeighbor == NegativeXNeighbor)
                    {
                        return 90f;
                    }

                    if (missingNeighbor == PositiveZNeighbor)
                    {
                        return 180f;
                    }

                    return missingNeighbor == PositiveXNeighbor
                        ? 270f
                        : 0f;
                default:
                    return 0f;
            }
        }

        private static bool HasMatchingNeighbor(
            IReadOnlyDictionary<GridCell, GameObject> placements,
            GridCell coordinate,
            GameObject prefab,
            int offsetX,
            int offsetZ)
        {
            var neighbor = new GridCell(
                coordinate.X + offsetX,
                coordinate.Z + offsetZ,
                coordinate.Y);
            return placements.TryGetValue(neighbor, out GameObject neighborPrefab)
                && neighborPrefab == prefab;
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
            switch (kind)
            {
                case BoardGeometryKind.PlacementSurface:
                    return BoardCellFlags.SupportsPlacement;
                case BoardGeometryKind.RoadSurface:
                    return BoardCellFlags.Road;
                case BoardGeometryKind.RoadSpawnSurface:
                    return BoardCellFlags.RoadSpawn;
                case BoardGeometryKind.RoadEndSurface:
                    return BoardCellFlags.RoadEnd;
                default:
                    return BoardCellFlags.StaticBlocker;
            }
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

        private static string BuildGridPlaceableSignature(
            GridDimensions dimensions,
            float cellSize,
            float heightUnit,
            IReadOnlyList<BoardGridPlaceableVisual> visuals)
        {
            var canonical = new StringBuilder("grid-placeable-v2|");
            canonical.Append(dimensions.Width).Append('|')
                .Append(dimensions.Height).Append('|')
                .Append(dimensions.Depth).Append('|')
                .Append(cellSize.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                .Append(heightUnit.ToString("R", CultureInfo.InvariantCulture));

            for (int index = 0; index < visuals.Count; index++)
            {
                BoardGridPlaceableVisual visual = visuals[index];
                string prefabPath = AssetDatabase.GetAssetPath(visual.Prefab);
                canonical.Append(';')
                    .Append(visual.Coordinate.X).Append(',')
                    .Append(visual.Coordinate.Y).Append(',')
                    .Append(visual.Coordinate.Z).Append(',')
                    .Append(AssetDatabase.AssetPathToGUID(prefabPath)).Append(',')
                    .Append(AssetDatabase.GetAssetDependencyHash(prefabPath)).Append(',')
                    .Append((int)visual.Topology).Append(',')
                    .Append((int)visual.Axis).Append(',')
                    .Append(visual.SortingOrder).Append(',');
                AppendVector3(canonical, visual.LocalPosition);
                canonical.Append(',');
                AppendQuaternion(canonical, visual.LocalRotation);
                canonical.Append(',');
                AppendVector3(canonical, visual.LocalScale);
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

        private static void AppendVector3(
            StringBuilder target,
            Vector3 value)
        {
            target.Append(value.x.ToString("R", CultureInfo.InvariantCulture))
                .Append(',')
                .Append(value.y.ToString("R", CultureInfo.InvariantCulture))
                .Append(',')
                .Append(value.z.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void AppendQuaternion(
            StringBuilder target,
            Quaternion value)
        {
            target.Append(value.x.ToString("R", CultureInfo.InvariantCulture))
                .Append(',')
                .Append(value.y.ToString("R", CultureInfo.InvariantCulture))
                .Append(',')
                .Append(value.z.ToString("R", CultureInfo.InvariantCulture))
                .Append(',')
                .Append(value.w.ToString("R", CultureInfo.InvariantCulture));
        }
    }
}
