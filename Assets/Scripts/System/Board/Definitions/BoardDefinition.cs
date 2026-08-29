using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.GridPlacement
{
    [CreateAssetMenu(fileName = "BoardDefinition", menuName = "Tower Defense/Grid Placement/Board Definition")]
    public sealed class BoardDefinition : ScriptableObject
    {
        [SerializeField] private GridDimensions dimensions = new GridDimensions(1, 1, 1);
        [SerializeField, Min(0.01f)] private float cellSize = 1f;
        [SerializeField, Min(0.01f)] private float heightUnit = 1f;
        [SerializeField] private bool visualizeInScene = true;
        [SerializeField, Min(0)] private int maxCameraGridXSpan = 0;
        [SerializeField, Min(0)] private int maxCameraGridYSpan = 0;
        [Tooltip("Applied after automatic framing in the Camera's right, up, and forward axes.")]
        [SerializeField] private Vector3 cameraPositionOffset;
        [Tooltip("Euler delta applied to the authored Camera rotation before framing.")]
        [SerializeField] private Vector3 cameraRotationOffsetEuler;
        [SerializeField] private BoardCellDefinition[] cells = Array.Empty<BoardCellDefinition>();
        [SerializeField] private GridPlaceablePlacement[] gridPlaceables =
            Array.Empty<GridPlaceablePlacement>();
        [Tooltip("Ordered walk per enemy route. Authored routes win over per-cell exit arrows.")]
        [SerializeField] private BoardRouteDefinition[] routes =
            Array.Empty<BoardRouteDefinition>();

        public GridDimensions Dimensions => dimensions;
        public float CellSize => cellSize;
        public float HeightUnit => heightUnit;
        public bool VisualizeInScene => visualizeInScene;
        public int MaxCameraGridXSpan => maxCameraGridXSpan;
        public int MaxCameraGridYSpan => maxCameraGridYSpan;
        public Vector3 CameraPositionOffset => cameraPositionOffset;
        public Vector3 CameraRotationOffsetEuler => cameraRotationOffsetEuler;
        public IReadOnlyList<BoardCellDefinition> Cells => cells;
        public IReadOnlyList<GridPlaceablePlacement> GridPlaceables => gridPlaceables;
        public IReadOnlyList<BoardRouteDefinition> Routes => routes;
    }
}
