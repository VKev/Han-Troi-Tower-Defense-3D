using System;
using TowerDefense3D.GridPlacement;
using UnityEngine;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Adapts the scene-owned placement controller to the level participant lifecycle.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GridPlacementSceneAdapter : MonoBehaviour, ILevelSceneParticipant
    {
        [SerializeField] private GridPlacementController placementController;

        public void Initialize(LevelSceneRuntimeContext context)
        {
            if (!context.IsValid)
            {
                throw new ArgumentException(
                    "Placement received an invalid level runtime context.",
                    nameof(context));
            }

            if (placementController == null)
            {
                throw new InvalidOperationException(
                    "GridPlacementSceneAdapter requires a GridPlacementController.");
            }

            placementController.Initialize();
        }

        public void Shutdown()
        {
            placementController?.Shutdown();
        }
    }
}
