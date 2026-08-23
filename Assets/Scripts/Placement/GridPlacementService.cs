using System;
using TowerDefense3D.Towers;
using UnityEngine;

namespace TowerDefense3D.GridPlacement
{
    /// <summary>
    /// Owns the board/occupancy/validator triad for one placement session and applies
    /// the tower placement business rules (evaluate, reserve, spawn, commit/rollback).
    /// </summary>
    internal sealed class GridPlacementService
    {
        private readonly BoardDefinition boardDefinition;
        private readonly GridBoard board;
        private readonly GridOccupancy occupancy;
        private readonly PlacementValidator validator;
        private int nextOwnerId = 1;

        internal GridPlacementService(BoardDefinition boardDefinition, Vector3 origin)
        {
            this.boardDefinition = boardDefinition;
            board = new GridBoard(boardDefinition, origin);
            occupancy = new GridOccupancy(boardDefinition.Dimensions);
            validator = new PlacementValidator(board, occupancy);
        }

        internal GridOccupancy Occupancy => occupancy;

        internal bool TryWorldToCell(Vector3 worldPoint, out GridCell cell)
        {
            return board.Mapper.TryWorldToCell(worldPoint, out cell);
        }

        internal PlacementResult Evaluate(GridCell cell, TowerFootprint footprint)
        {
            return validator.Evaluate(cell, footprint);
        }

        internal Vector3 GetFootprintBottomCenter(GridCell anchor, TowerFootprint footprint)
        {
            Vector3 center = board.Mapper.CellToWorldCenter(anchor);
            if ((footprint.Width & 1) == 0)
            {
                center.x += boardDefinition.CellSize * 0.5f;
            }

            if ((footprint.Depth & 1) == 0)
            {
                center.z += boardDefinition.CellSize * 0.5f;
            }

            return center;
        }

        internal bool TryPlace(
            GridCell cell,
            TowerDefinition tower,
            TowerCombatDefinition combatDefinition,
            Transform placedObjectsRoot,
            out TowerPlacementRecord? placement)
        {
            placement = null;
            if (tower == null || tower.Prefab == null)
            {
                return false;
            }

            PlacementResult currentResult = validator.Evaluate(cell, tower.Footprint);
            if (!currentResult.Succeeded
                || !occupancy.TryReserve(cell, tower.Footprint, out PlacementReservation reservation))
            {
                return false;
            }

            GameObject instance = null;
            TowerRuntimeView runtimeView = null;
            int ownerId = 0;
            using (reservation)
            {
                try
                {
                    instance = UnityEngine.Object.Instantiate(
                        tower.Prefab,
                        GetFootprintBottomCenter(cell, tower.Footprint),
                        tower.Prefab.transform.rotation,
                        placedObjectsRoot);

                    if (instance == null)
                    {
                        return false;
                    }

                    ownerId = NextOwnerId();
                    if (combatDefinition != null)
                    {
                        runtimeView = instance.GetComponent<TowerRuntimeView>();
                        if (runtimeView == null)
                        {
                            runtimeView = instance.AddComponent<TowerRuntimeView>();
                        }

                        runtimeView.Configure(combatDefinition);
                    }

                    if (!reservation.Commit(ownerId))
                    {
                        UnityEngine.Object.Destroy(instance);
                        return false;
                    }
                }
                catch (Exception exception)
                {
                    if (instance != null)
                    {
                        UnityEngine.Object.Destroy(instance);
                    }

                    Debug.LogException(exception);
                    return false;
                }
            }

            if (runtimeView != null)
            {
                placement = new TowerPlacementRecord(combatDefinition, tower, runtimeView, cell, ownerId);
            }

            return true;
        }

        private int NextOwnerId()
        {
            if (nextOwnerId == int.MaxValue)
            {
                nextOwnerId = 1;
            }

            return nextOwnerId++;
        }
    }
}
