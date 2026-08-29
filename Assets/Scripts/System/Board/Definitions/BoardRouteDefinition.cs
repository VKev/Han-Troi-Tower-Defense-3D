using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.GridPlacement
{
    /// <summary>
    /// One authored enemy route, stored as the ordered walk the enemy takes over board cells.
    /// An ordered walk can step onto the same cell twice, which a per-cell exit arrow cannot:
    /// that is what lets a route lap a closed loop, and lets two routes leave one junction in
    /// different directions.
    /// </summary>
    [Serializable]
    public struct BoardRouteDefinition
    {
        [SerializeField] private GridCell[] cells;

        public BoardRouteDefinition(IReadOnlyList<GridCell> cells)
        {
            if (cells == null)
            {
                throw new ArgumentNullException(nameof(cells));
            }

            this.cells = new GridCell[cells.Count];
            for (int index = 0; index < cells.Count; index++)
            {
                this.cells[index] = cells[index];
            }
        }

        public IReadOnlyList<GridCell> Cells => cells ?? Array.Empty<GridCell>();
    }
}
