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
        [SerializeField] private BoardCellDefinition[] cells = Array.Empty<BoardCellDefinition>();

        public GridDimensions Dimensions => dimensions;
        public float CellSize => cellSize;
        public float HeightUnit => heightUnit;
        public IReadOnlyList<BoardCellDefinition> Cells => cells;
    }
}
